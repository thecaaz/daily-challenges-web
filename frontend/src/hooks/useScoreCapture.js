import { useState, useEffect, useRef } from 'react'
import compressImage from '../utils/compressImage'

export function useScoreCapture(iframeRef, { isMaximized } = {}) {
  const [score, setScore] = useState('')
  const [capturedFile, setCapturedFile] = useState(null)
  const [showSubmission, setShowSubmission] = useState(false)

  const lastNonceRef = useRef(null)
  const captureStateRef = useRef({ nonce: null, fauxFullscreen: false })

  useEffect(() => {
    async function onMessage(ev) {
      if (!ev.data || typeof ev.data.type !== 'string') return
      const data = ev.data

      if (data.type === 'SCORE_RESPONSE') {
        if (data.nonce && lastNonceRef.current && data.nonce !== lastNonceRef.current) return
        setScore(typeof data.score === 'string' ? data.score : JSON.stringify(data.score))
      }

      if (data.type === 'CAPTURE_RESPONSE') {
        if (data.nonce && captureStateRef.current.nonce && data.nonce !== captureStateRef.current.nonce) return

        if (data.error) {
          if (captureStateRef.current.fauxFullscreen) {
            const container = iframeRef.current?.parentElement
            if (container && container.__prevStyle) {
              Object.assign(container.style, container.__prevStyle)
              delete container.__prevStyle
            }
            captureStateRef.current.fauxFullscreen = false
          }
          return
        }

        if (data.dataUrl && data.rect) {
          try {
            const file = await compressImage(data.dataUrl)
            setCapturedFile(file)

            if (captureStateRef.current.fauxFullscreen) {
              const container = iframeRef.current?.parentElement
              if (container && container.__prevStyle) {
                Object.assign(container.style, container.__prevStyle)
                delete container.__prevStyle
              }
              captureStateRef.current.fauxFullscreen = false
            }
            // don't set showSubmission here — let the effect below decide when both score and file are present
          } catch (err) {
            // ignore
          }
        }
      }
    }

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [iframeRef])

  useEffect(() => {
    if (score && capturedFile) {
      if (captureStateRef.current.fauxFullscreen) return
      setShowSubmission(true)
    }
  }, [score, capturedFile])

  function postToIframe(payload) {
    const iframe = iframeRef.current
    if (!iframe?.contentWindow) return
    const origin = (function () {
      try {
        return new URL(iframe.src).origin
      } catch (e) {
        return '*'
      }
    })()
    iframe.contentWindow.postMessage(payload, origin)
  }

  async function submitScore() {
    const nonce = Math.random().toString(36).slice(2)
    lastNonceRef.current = nonce
    captureStateRef.current.nonce = nonce
    const iframe = iframeRef.current
    if (iframe && iframe.scrollIntoView) {
      try { iframe.scrollIntoView({ behavior: 'smooth', block: 'center' }) } catch (e) { iframe.scrollIntoView() }
    }

    postToIframe({ source: 'ScoreBridgeParent', type: 'GET_SCORE', nonce })

    try {
      const container = iframe?.parentElement
      if (container && !isMaximized) {
        container.__prevStyle = {
          position: container.style.position || '',
          zIndex: container.style.zIndex || '',
          left: container.style.left || '',
          top: container.style.top || '',
          width: container.style.width || '',
          height: container.style.height || ''
        }
        container.style.position = 'fixed'
        container.style.left = '0'
        container.style.top = '0'
        container.style.width = '100vw'
        container.style.height = '100vh'
        container.style.zIndex = '2147483647'
        captureStateRef.current.fauxFullscreen = true
        await new Promise(r => setTimeout(r, 300))
      } else {
        await new Promise(r => setTimeout(r, 300))
      }
    } catch (err) {
      await new Promise(r => setTimeout(r, 300))
    }

    postToIframe({ source: 'ScoreBridgeParent', type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce })
  }

  return { score, setScore, capturedFile, setCapturedFile, showSubmission, setShowSubmission, submitScore }
}
