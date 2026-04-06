import React, { useEffect, useState, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Box, CircularProgress, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'

export default function Play() {
  const { gameId } = useParams()
  const navigate = useNavigate()
  const { user, loading: authLoading, fetchMe } = useRequireAuth()

  const [game, setGame] = useState(null)
  const [loading, setLoading] = useState(true)

  const iframeRef = useRef(null)
  const [score, setScore] = useState('')
  const [showSubmission, setShowSubmission] = useState(false)
  const [capturedFile, setCapturedFile] = useState(null)
  const lastNonceRef = useRef(null)
  const captureStateRef = useRef({ nonce: null, fauxFullscreen: false })

  useEffect(() => {
    async function onMessage(ev) {
      if (!ev.data || typeof ev.data.type !== 'string') return
      const data = ev.data

      if (data.type === 'SCORE_RESPONSE') {
        if (data.nonce && lastNonceRef.current && data.nonce !== lastNonceRef.current) return
        // store as string to populate input
        setScore(typeof data.score === 'string' ? data.score : JSON.stringify(data.score))
        console.debug('SCORE_RESPONSE', data.score)
      }

      if (data.type === 'CAPTURE_RESPONSE') {
        if (data.nonce && captureStateRef.current.nonce && data.nonce !== captureStateRef.current.nonce) return

        if (data.error) {
          console.debug('CAPTURE_RESPONSE error', data.error)
          // revert faux-fullscreen styles if applied
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
            // convert dataUrl to File and set as captured file for the submission form
            const file = dataUrlToFile(data.dataUrl, 'capture.png')
            setCapturedFile(file)
            console.debug('CAPTURE_RESPONSE', { rect: data.rect, dpr: data.dpr })

            // revert faux-fullscreen styles if applied, then show submission
            if (captureStateRef.current.fauxFullscreen) {
              const container = iframeRef.current?.parentElement
              if (container && container.__prevStyle) {
                Object.assign(container.style, container.__prevStyle)
                delete container.__prevStyle
              }
              captureStateRef.current.fauxFullscreen = false
            }
            if (score) setShowSubmission(true)
          } catch (err) {
            console.debug('Capture processing error', err)
          }
        }
      }
    }

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [])

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        const res = await api.get(`/games/${gameId}`)
        if (mounted) setGame(res.data)
      } catch (err) {
        console.error('Failed to load game', err)
      } finally {
        if (mounted) setLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [gameId])

  // show submission UI when both score and screenshot are available
  useEffect(() => {
    if (score && capturedFile) {
      // if we used faux-fullscreen for capture, wait for the capture handler to revert styles
      if (captureStateRef.current.fauxFullscreen) return
      setShowSubmission(true)
    }
  }, [score, capturedFile])

  if (loading) return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>

  if (!game) return <Typography sx={{ mt: 4 }}>Game not found.</Typography>

  const url = game.url

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
      try {
        iframe.scrollIntoView({ behavior: 'smooth', block: 'center' })
      } catch (e) {
        iframe.scrollIntoView()
      }
    }

    // request score immediately
    postToIframe({ source: 'ScoreBridgeParent', type: 'GET_SCORE', nonce })

    // show iframe over everything else (faux-fullscreen) so capture contains iframe only
    try {
      const container = iframe?.parentElement
      if (container) {
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
      console.debug('Failed to apply faux-fullscreen for capture', err)
      await new Promise(r => setTimeout(r, 300))
    }

    // ask the iframe to capture the visible tab
    postToIframe({ source: 'ScoreBridgeParent', type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce })
  }


  function dataUrlToFile(dataUrl, filename) {
    const arr = dataUrl.split(',')
    const mimeMatch = arr[0].match(/:(.*?);/)
    const mime = mimeMatch ? mimeMatch[1] : 'image/png'
    const bstr = atob(arr[1])
    let n = bstr.length
    const u8arr = new Uint8Array(n)
    while (n--) {
      u8arr[n] = bstr.charCodeAt(n)
    }
    return new File([u8arr], filename, { type: mime })
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h5">{game.name}</Typography>
        {url && (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <AppButton href={url} target="_blank" rel="noreferrer" variant="outlined">Open in new tab</AppButton>
            <AppButton onClick={submitScore} variant="contained">Submit score</AppButton>
          </Box>
        )}
      </Box>

      {!url ? (
        <Typography>No playable URL for this game.</Typography>
      ) : (
        <>
          {!showSubmission ? (
            <Box sx={{ width: '100vw', marginLeft: 'calc(50% - 50vw)', height: '80vh' }}>
              <iframe allowFullScreen allow="fullscreen" ref={iframeRef} src={url} title={game.name || 'Play'} style={{ width: '100%', height: '100%', border: 0 }} />
            </Box>
          ) : (
            <SubmissionForm
              gameId={gameId}
              initialScore={score}
              initialScreenshot={capturedFile}
              hasSubmitted={game?.hasSubmittedForLatest}
              onCancel={() => { setShowSubmission(false); setCapturedFile(null); setScore('') }}
            />
          )}
        </>
      )}
    </Box>
  )
}
