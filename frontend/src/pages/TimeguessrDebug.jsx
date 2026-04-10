import React, { useRef, useEffect, useState } from 'react'
import { Box, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'

function genNonce() {
  return Math.random().toString(36).slice(2)
}

export default function TimeguessrDebug() {
  const iframeRef = useRef(null)
  const [score, setScore] = useState(null)
  const [preview, setPreview] = useState(null)
  const lastNonceRef = useRef(null)
  const captureStateRef = useRef({ nonce: null })

  useEffect(() => {
    function onMessage(ev) {
      if (!ev.data || typeof ev.data.type !== 'string') return
      const data = ev.data

      if (data.type === 'SCORE_RESPONSE') {
        if (data.nonce && lastNonceRef.current && data.nonce !== lastNonceRef.current) return
        setScore(data.score)
        console.debug('SCORE_RESPONSE', data.score)
      }

      if (data.type === 'CAPTURE_RESPONSE') {
        if (data.nonce && captureStateRef.current.nonce && data.nonce !== captureStateRef.current.nonce) return

        if (data.error) {
          console.debug('CAPTURE_RESPONSE error', data.error)
          return
        }

        if (data.dataUrl && data.rect) {
          const img = new Image()
          img.onload = function () {
            try {
              const dpr = data.dpr || 1
              const sx = Math.round((data.rect.left || 0) * dpr)
              const sy = Math.round((data.rect.top || 0) * dpr)
              const sw = Math.max(1, Math.round((data.rect.width || 0) * dpr))
              const sh = Math.max(1, Math.round((data.rect.height || 0) * dpr))
              const out = document.createElement('canvas')
              out.width = sw
              out.height = sh
              const ctx = out.getContext('2d')
              ctx.drawImage(img, sx, sy, sw, sh, 0, 0, sw, sh)
              const final = out.toDataURL('image/png')
              setPreview(final)
              console.debug('CAPTURE_RESPONSE', { rect: data.rect, dpr: data.dpr, dataUrl: final })
            } catch (err) {
              console.debug('Render error', err)
              setPreview(null)
            }
          }
          img.onerror = function () {
            console.debug('Image load error')
            setPreview(null)
          }
          img.src = data.dataUrl
        }
      }
    }

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [])

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

  function submitScore() {
    const nonce = genNonce()
    lastNonceRef.current = nonce
    captureStateRef.current.nonce = nonce
    const iframe = iframeRef.current
    if (iframe && iframe.scrollIntoView) {
      try {
        iframe.scrollIntoView({ behavior: 'smooth', block: 'center' })
      } catch (e) {
        iframe.scrollIntoView()
      }
    }

    postToIframe({ source: 'ScoreBridgeParent', type: 'GET_SCORE', nonce })
    setTimeout(() => {
      postToIframe({ source: 'ScoreBridgeParent', type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce })
    }, 500)
  }

  return (
    <Box sx={{ mt: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Typography variant="h5">Debug Timeguessr Test</Typography>
        <AppButton onClick={submitScore}>Submit score</AppButton>
      </Box>
      <Box sx={{ mb: 2, width: '100vw', marginLeft: 'calc(50% - 50vw)' }}>
        <iframe
          ref={iframeRef}
          src="https://timeguessr.com/roundonedaily"
          style={{ width: '100%', height: 800, border: 0 }}
          title="Timeguessr"
        />
      </Box>
      {score && <pre style={{ whiteSpace: 'pre-wrap', marginTop: 12 }}>{JSON.stringify(score, null, 2)}</pre>}
      {preview && <img src={preview} alt="capture preview" style={{ maxWidth: 480, display: 'block', border: '1px solid #ddd', padding: 4, marginTop: 12 }} />}
    </Box>
  )
}
