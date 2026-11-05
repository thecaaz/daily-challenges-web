import { useState, useEffect, useRef, useCallback } from 'react'
import compressImage from '../utils/compressImage'
import useAdapter from './useAdapter'

// How long the faux-fullscreen overlay may stay up waiting for the extension.
export const CAPTURE_TIMEOUT_MS = 3000
// Let the overlay layout settle before the screenshot is taken.
const OVERLAY_SETTLE_MS = 300

function originOf(url) {
  try {
    const origin = new URL(url).origin
    return origin && origin !== 'null' ? origin : null
  } catch (e) {
    return null
  }
}

function makeNonce() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}${Math.random().toString(36).slice(2)}`
}

/** Score from the extension -> form value. Anything not a usable score -> ''. */
export function normalizeCapturedScore(value) {
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) return ''
    const s = String(value)
    // Avoid exponent notation (1e+21) in the form.
    return /e/i.test(s) ? value.toLocaleString('en-US', { useGrouping: false, maximumFractionDigits: 20 }) : s
  }
  if (typeof value === 'string') {
    const s = value.trim()
    return s.length > 0 && s.length <= 64 ? s : ''
  }
  return ''
}

/**
 * Score + screenshot capture through the ScoreBridge extension.
 *
 * @param {React.RefObject<HTMLIFrameElement>} iframeRef - the game iframe
 * @param {object} [options]
 * @param {boolean} [options.isMaximized] - iframe already fills the screen (no faux fullscreen)
 * @param {string}  [options.url] - game URL; used to decide `canCapture`
 *
 * Returns the original fields (score, setScore, capturedFile, setCapturedFile,
 * showSubmission, setShowSubmission, submitScore) plus:
 * - canCapture: an extension adapter exists for `options.url` (hide "Submit score" when false)
 * - capturing: a capture is in progress
 * - captureError: error text from the last failed capture, or null
 * - cancelCapture(): abandon the capture in progress (e.g. the user skipped the
 *   game); late responses for it are ignored
 */
export function useScoreCapture(iframeRef, { isMaximized, url } = {}) {
  const [score, setScore] = useState('')
  const [capturedFile, setCapturedFile] = useState(null)
  const [showSubmission, setShowSubmission] = useState(false)
  const [capturing, setCapturing] = useState(false)
  const [captureError, setCaptureError] = useState(null)

  const adapter = useAdapter(url)
  const canCapture = !!adapter

  // The pending score request: { nonce, source, origin }. The game window and
  // its origin are snapshotted when the request is sent, so a score that
  // arrives after the iframe unmounted (the form replaces it) still counts.
  const scoreRequestRef = useRef(null)
  const captureNonceRef = useRef(null)
  // Bumped on every new capture and on cancel; an async step that started
  // under an older id must not touch state.
  const captureIdRef = useRef(0)
  const overlayRef = useRef(null) // { container, prevStyle }
  const timeoutRef = useRef(null)
  const mountedRef = useRef(true)
  const isMaximizedRef = useRef(isMaximized)
  isMaximizedRef.current = isMaximized

  const restoreOverlay = useCallback(() => {
    const overlay = overlayRef.current
    overlayRef.current = null
    if (overlay) Object.assign(overlay.container.style, overlay.prevStyle)
  }, [])

  const applyOverlay = useCallback(() => {
    const container = iframeRef.current?.parentElement
    if (!container || overlayRef.current) return false
    const s = container.style
    overlayRef.current = {
      container,
      prevStyle: { position: s.position, zIndex: s.zIndex, left: s.left, top: s.top, width: s.width, height: s.height }
    }
    Object.assign(s, { position: 'fixed', left: '0', top: '0', width: '100vw', height: '100vh', zIndex: '2147483647' })
    return true
  }, [iframeRef])

  // End the current capture: always restores the overlay and stops the timer.
  const finishCapture = useCallback(({ file = null, error = null } = {}) => {
    clearTimeout(timeoutRef.current)
    timeoutRef.current = null
    captureNonceRef.current = null
    restoreOverlay()
    if (!mountedRef.current) return
    setCapturing(false)
    if (file) setCapturedFile(file)
    setCaptureError(error)
    // Show the form even without a score (or screenshot) so the user can
    // fill in what's missing instead of being stuck.
    setShowSubmission(true)
  }, [restoreOverlay])

  // Abandon the capture in progress: drop both nonces (late responses are
  // ignored), stop the timer, restore the overlay. Does not open the form.
  const cancelCapture = useCallback(() => {
    captureIdRef.current += 1
    clearTimeout(timeoutRef.current)
    timeoutRef.current = null
    captureNonceRef.current = null
    scoreRequestRef.current = null
    restoreOverlay()
    if (!mountedRef.current) return
    setCapturing(false)
    setCaptureError(null)
  }, [restoreOverlay])

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      captureIdRef.current += 1
      clearTimeout(timeoutRef.current)
      timeoutRef.current = null
      captureNonceRef.current = null
      scoreRequestRef.current = null
      restoreOverlay()
    }
  }, [restoreOverlay])

  useEffect(() => {
    async function onMessage(ev) {
      const data = ev.data
      if (!data || typeof data.type !== 'string') return

      if (data.type === 'SCORE_RESPONSE') {
        // Only from the game window we asked, at its origin, for our request.
        const req = scoreRequestRef.current
        if (!req || !req.source || ev.source !== req.source) return
        if (!req.origin || ev.origin !== req.origin) return
        if (!req.nonce || data.nonce !== req.nonce) return
        scoreRequestRef.current = null
        setScore(normalizeCapturedScore(data.score))
        return
      }

      if (data.type === 'CAPTURE_RESPONSE') {
        // Only from the extension's content script in this window.
        if (ev.source !== window || ev.origin !== window.location.origin) return
        if (!captureNonceRef.current || data.nonce !== captureNonceRef.current) return
        captureNonceRef.current = null
        const captureId = captureIdRef.current

        let file = null
        let error = data.error ? String(data.error) : null
        try {
          if (!error && typeof data.dataUrl === 'string' && data.dataUrl.startsWith('data:image/')) {
            restoreOverlay() // screenshot is taken; don't keep the overlay up while compressing
            file = await compressImage(data.dataUrl)
          } else if (!error) {
            error = 'capture returned no image'
          }
        } catch (err) {
          error = 'could not process the screenshot'
        } finally {
          // Cancelled (or a new capture started) while compressing: drop it.
          if (captureIdRef.current === captureId) finishCapture({ file, error })
        }
      }
    }

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [iframeRef, restoreOverlay, finishCapture])

  async function submitScore() {
    if (captureNonceRef.current) return // a capture is already running
    const iframe = iframeRef.current
    const targetOrigin = iframe ? originOf(iframe.src) : null
    if (!iframe?.contentWindow || !targetOrigin) return

    const scoreNonce = makeNonce()
    const captureNonce = makeNonce()
    const gameWindow = iframe.contentWindow
    captureIdRef.current += 1
    scoreRequestRef.current = { nonce: scoreNonce, source: gameWindow, origin: targetOrigin }
    captureNonceRef.current = captureNonce
    setScore('')
    setCapturedFile(null)
    setCaptureError(null)
    setCapturing(true)

    try {
      if (iframe.scrollIntoView) {
        try { iframe.scrollIntoView({ behavior: 'smooth', block: 'center' }) } catch (e) { iframe.scrollIntoView() }
      }

      gameWindow.postMessage({ source: 'ScoreBridgeParent', type: 'GET_SCORE', nonce: scoreNonce }, targetOrigin)

      if (!isMaximizedRef.current) applyOverlay()
      timeoutRef.current = setTimeout(() => {
        if (captureNonceRef.current === captureNonce) finishCapture({ error: 'the extension did not respond' })
      }, CAPTURE_TIMEOUT_MS)

      await new Promise(r => setTimeout(r, OVERLAY_SETTLE_MS))
      if (!mountedRef.current || captureNonceRef.current !== captureNonce) return

      // Arm the extension for exactly this capture, then ask the game frame for it.
      window.postMessage({ type: 'SCOREBRIDGE_ARM', nonce: captureNonce }, window.location.origin)
      gameWindow.postMessage({ source: 'ScoreBridgeParent', type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce: captureNonce }, targetOrigin)
    } catch (err) {
      if (captureNonceRef.current === captureNonce) finishCapture({ error: 'could not request a capture' })
    }
  }

  return {
    score,
    setScore,
    capturedFile,
    setCapturedFile,
    showSubmission,
    setShowSubmission,
    submitScore,
    cancelCapture,
    canCapture,
    capturing,
    captureError
  }
}
