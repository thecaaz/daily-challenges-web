import { useState, useEffect, useRef, useCallback } from 'react'
import useAdapter from './useAdapter'
import useFauxFullscreen from './useFauxFullscreen'
import { CaptureError, normalizeCapturedScore, readCapturedScreenshot } from '../utils/capturedScore'
import {
  MessageType, makeNonce, isFromContentScript, postToGameFrame,
  gameFrameOf, isResponseToScoreRequest, requestScreenshot
} from '../utils/scoreBridge'

export { normalizeCapturedScore }

/** How long the faux-fullscreen overlay may stay up waiting for the extension. */
export const CAPTURE_TIMEOUT_MS = 3000
/** Lets the overlay layout settle before the screenshot is taken. */
const OVERLAY_SETTLE_MS = 300
const SCROLL_TO_CENTER = { behavior: 'smooth', block: 'center' }

const delay = ms => new Promise(resolve => setTimeout(resolve, ms))

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
 *
 * The pending score request snapshots the game window and origin when it is
 * sent, so a score that arrives after the iframe unmounted (the form replaces
 * it) still counts. `captureIdRef` changes on every new capture, cancel and
 * unmount: an async step that started under an older id must not touch state.
 * The form opens after every finished capture, even without a score or
 * screenshot, so the user can fill in what is missing.
 */
export function useScoreCapture(iframeRef, { isMaximized, url } = {}) {
  const [score, setScore] = useState('')
  const [capturedFile, setCapturedFile] = useState(null)
  const [showSubmission, setShowSubmission] = useState(false)
  const [capturing, setCapturing] = useState(false)
  const [captureError, setCaptureError] = useState(null)

  const canCapture = !!useAdapter(url)
  const { apply: applyOverlay, restore: restoreOverlay } = useFauxFullscreen(iframeRef)

  const scoreRequestRef = useRef(null)
  const captureNonceRef = useRef(null)
  const captureIdRef = useRef(0)
  const timeoutRef = useRef(null)
  const mountedRef = useRef(true)
  const isMaximizedRef = useRef(isMaximized)
  isMaximizedRef.current = isMaximized

  const endCaptureRequest = useCallback(() => {
    clearTimeout(timeoutRef.current)
    timeoutRef.current = null
    captureNonceRef.current = null
    restoreOverlay()
  }, [restoreOverlay])

  const abandonCaptureRequest = useCallback(() => {
    captureIdRef.current += 1
    scoreRequestRef.current = null
    endCaptureRequest()
  }, [endCaptureRequest])

  const finishCapture = useCallback(({ file = null, error = null } = {}) => {
    endCaptureRequest()
    if (!mountedRef.current) return
    setCapturing(false)
    if (file) setCapturedFile(file)
    setCaptureError(error)
    setShowSubmission(true)
  }, [endCaptureRequest])

  const cancelCapture = useCallback(() => {
    abandonCaptureRequest()
    if (!mountedRef.current) return
    setCapturing(false)
    setCaptureError(null)
  }, [abandonCaptureRequest])

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      abandonCaptureRequest()
    }
  }, [abandonCaptureRequest])

  useEffect(() => {
    function handleScoreResponse(event) {
      if (!isResponseToScoreRequest(event, scoreRequestRef.current)) return
      scoreRequestRef.current = null
      setScore(normalizeCapturedScore(event.data.score))
    }

    async function handleCaptureResponse(event) {
      if (!isFromContentScript(event) || !isCurrentCaptureNonce(captureNonceRef.current, event.data.nonce)) return
      captureNonceRef.current = null
      const captureId = captureIdRef.current
      const result = await readCapturedScreenshot(event.data, restoreOverlay)
      if (captureIdRef.current === captureId) finishCapture(result)
    }

    const handlers = new Map([
      [MessageType.SCORE_RESPONSE, handleScoreResponse],
      [MessageType.CAPTURE_RESPONSE, handleCaptureResponse],
    ])
    const onMessage = event => handlers.get(event.data?.type)?.(event)

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [restoreOverlay, finishCapture])

  async function submitScore() {
    if (captureNonceRef.current) return
    const iframe = iframeRef.current
    const gameFrame = gameFrameOf(iframe)
    if (!gameFrame) return

    const scoreNonce = makeNonce()
    const captureNonce = makeNonce()
    captureIdRef.current += 1
    scoreRequestRef.current = { nonce: scoreNonce, source: gameFrame.gameWindow, origin: gameFrame.origin }
    captureNonceRef.current = captureNonce
    setScore('')
    setCapturedFile(null)
    setCaptureError(null)
    setCapturing(true)

    const isStillRunning = () => captureNonceRef.current === captureNonce
    try {
      scrollToCenter(iframe)
      postToGameFrame(gameFrame, { type: MessageType.GET_SCORE, nonce: scoreNonce })
      if (!isMaximizedRef.current) applyOverlay()
      timeoutRef.current = setTimeout(() => {
        if (isStillRunning()) finishCapture({ error: CaptureError.NO_RESPONSE })
      }, CAPTURE_TIMEOUT_MS)

      await delay(OVERLAY_SETTLE_MS)
      if (!mountedRef.current || !isStillRunning()) return

      requestScreenshot(gameFrame, captureNonce)
    } catch {
      if (isStillRunning()) finishCapture({ error: CaptureError.REQUEST_FAILED })
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

function isCurrentCaptureNonce(expected, received) {
  return !!expected && received === expected
}

function scrollToCenter(iframe) {
  if (!iframe.scrollIntoView) return
  try {
    iframe.scrollIntoView(SCROLL_TO_CENTER)
  } catch {
    iframe.scrollIntoView()
  }
}
