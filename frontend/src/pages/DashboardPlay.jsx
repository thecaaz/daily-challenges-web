import React, { useEffect, useRef, useState } from 'react'
import { Box, CircularProgress, Typography, Grid } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'
import { hasAdapterForUrl } from '../utils/adapters'
import GameCard from '../components/ui/GameCard/GameCard'
import { useNavigate } from 'react-router-dom'

export default function DashboardPlay() {
  const { user, loading: authLoading } = useRequireAuth()
  const navigate = useNavigate()

  const [loadingGames, setLoadingGames] = useState(true)
  const [queue, setQueue] = useState([])
  const [currentIndex, setCurrentIndex] = useState(0)
  const loadedQueueRef = useRef(false)

  const iframeRef = useRef(null)
  const [score, setScore] = useState('')
  const [showSubmission, setShowSubmission] = useState(false)
  const [capturedFile, setCapturedFile] = useState(null)
  const lastNonceRef = useRef(null)
  const captureStateRef = useRef({ nonce: null, fauxFullscreen: false })

  useEffect(() => {
    let mounted = true
    if (loadedQueueRef.current) {
      // already loaded once; do not reload the queue
      if (mounted) setLoadingGames(false)
      return () => { mounted = false }
    }

    async function load() {
      // only attempt to load when a user exists
      if (!user) return
      try {
        const res = await api.get('/games')
        const all = Array.isArray(res.data) ? res.data : []
        const playable = []
        for (const g of all) {
          try {
            if (!g.isFavorite) continue
            if (!g.url) continue
            if (g.hasSubmittedForLatest) continue
            const ok = await hasAdapterForUrl(g.url)
            if (ok) playable.push(g)
          } catch (e) {
            // ignore per-game
          }
        }
        playable.sort((a, b) => Number(a.id) - Number(b.id))
        if (mounted) {
          setQueue(playable)
          loadedQueueRef.current = true
        }
      } catch (e) {
        if (mounted) setQueue([])
      } finally {
        if (mounted) setLoadingGames(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [user])

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
            const file = dataUrlToFile(data.dataUrl, 'capture.png')
            setCapturedFile(file)

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
            // ignore
          }
        }
      }
    }

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [score])

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
      await new Promise(r => setTimeout(r, 300))
    }

    postToIframe({ source: 'ScoreBridgeParent', type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce })
  }

  function dataUrlToFile(dataUrl, filename) {
    const arr = dataUrl.split(',')
    const mimeMatch = arr[0].match(/:(.*?);/)
    const mime = mimeMatch ? mimeMatch[1] : 'image/png'
    const bstr = atob(arr[1])
    let n = bstr.length
    const u8arr = new Uint8Array(n)
    while (n--) u8arr[n] = bstr.charCodeAt(n)
    return new File([u8arr], filename, { type: mime })
  }

  const current = queue[currentIndex]

  const handleSubmitted = ({ submission }) => {
    // mark current as submitted and advance
    setQueue(prev => prev.map((g, i) => i === currentIndex ? { ...g, hasSubmittedForLatest: true } : g))
    setScore('')
    setCapturedFile(null)
    setShowSubmission(false)
    setCurrentIndex(i => i + 1)
  }

  if (loadingGames || authLoading) return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>

  if (!queue || queue.length === 0) {
    return (
      <Box>
        <Typography variant="h5" sx={{ mb: 2 }}>Daily Player</Typography>
        <Typography sx={{ mb: 2 }} color="text.secondary">No playable favorite games found. Make sure you have favorites and the browser extension supports the game.</Typography>
        <AppButton to="/dashboard" variant="contained">Back to Dashboard</AppButton>
      </Box>
    )
  }

  if (currentIndex >= queue.length) {
    return (
      <Box>
        <Typography variant="h5" sx={{ mb: 2 }}>All done</Typography>
        <Typography sx={{ mb: 2 }} color="text.secondary">You have finished the available playable favorites for today.</Typography>
        <AppButton to="/dashboard" variant="contained">Back to Dashboard</AppButton>
      </Box>
    )
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h5">{current.name}</Typography>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
          <Typography sx={{ alignSelf: 'center' }}>{currentIndex + 1} / {queue.length}</Typography>
          {current.url && (
            <AppButton href={current.url} target="_blank" rel="noreferrer" variant="outlined">Open in new tab</AppButton>
          )}
          {current.url && (
            <AppButton onClick={submitScore} variant="contained">Submit score</AppButton>
          )}
          <AppButton color="inherit" variant="outlined" onClick={() => setCurrentIndex(i => i + 1)}>Skip</AppButton>
          <AppButton to="/dashboard" variant="text">Quit</AppButton>
        </Box>
      </Box>

      {!current.url ? (
        <Typography>No playable URL for this game.</Typography>
      ) : (
        <>
          {!showSubmission ? (
            <Box sx={{ width: '100vw', marginLeft: 'calc(50% - 50vw)', height: '80vh' }}>
              <iframe allowFullScreen allow="fullscreen" ref={iframeRef} src={current.url} title={current.name || 'Play'} style={{ width: '100%', height: '100%', border: 0 }} />
            </Box>
          ) : (
            <SubmissionForm
              gameId={current.id}
              initialScore={score}
              initialScreenshot={capturedFile}
              hasSubmitted={current?.hasSubmittedForLatest}
              onCancel={() => { setShowSubmission(false); setCapturedFile(null); setScore('') }}
              onSubmitted={handleSubmitted}
            />
          )}
        </>
      )}

      
    </Box>
  )
}
