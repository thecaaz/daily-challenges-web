import React, { useEffect, useRef, useState } from 'react'
import { Box, CircularProgress, IconButton, Tooltip, Typography, Grid } from '@mui/material'
import FullscreenIcon from '@mui/icons-material/Fullscreen'
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import SkipNextIcon from '@mui/icons-material/SkipNext'
import ExitToAppIcon from '@mui/icons-material/ExitToApp'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'
import { hasAdapterForUrl } from '../utils/adapters'
import GameCard from '../components/ui/GameCard/GameCard'
import { useNavigate } from 'react-router-dom'
import { useScoreCapture } from '../hooks/useScoreCapture'
import GameIframePlayer from '../components/ui/GameIframePlayer'

export default function DashboardPlay() {
  const { user, loading: authLoading } = useRequireAuth()
  const navigate = useNavigate()

  const [loadingGames, setLoadingGames] = useState(true)
  const [queue, setQueue] = useState([])
  const [currentIndex, setCurrentIndex] = useState(0)
  const loadedQueueRef = useRef(false)

  const iframeRef = useRef(null)
  const [isMaximized, setIsMaximized] = useState(false)
  const { score, setScore, capturedFile, setCapturedFile, showSubmission, setShowSubmission, submitScore } = useScoreCapture(iframeRef, { isMaximized })

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

  // message handling and capture logic moved to `useScoreCapture` hook

  // submitScore provided by the hook

  const current = queue[currentIndex]

  // iframe load state handled by GameIframePlayer

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
            <Tooltip title="Open in new tab">
              <IconButton href={current.url} target="_blank" rel="noreferrer" size="small">
                <OpenInNewIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
          {current.url && (
            <AppButton onClick={submitScore} variant="contained" size="small">Submit</AppButton>
          )}
          {current.url && !isMaximized && (
            <Tooltip title="Maximize">
              <IconButton onClick={() => setIsMaximized(true)} size="small">
                <FullscreenIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
          <Tooltip title="Skip">
            <IconButton size="small" onClick={() => setCurrentIndex(i => i + 1)}>
              <SkipNextIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Quit">
            <IconButton size="small" onClick={() => navigate('/dashboard')}>
              <ExitToAppIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      {!current.url ? (
        <Typography>No playable URL for this game.</Typography>
      ) : (
        <>
          {!showSubmission ? (
            <GameIframePlayer src={current.url} title={current.name} iframeRef={iframeRef} isMaximized={isMaximized} onRestore={() => setIsMaximized(false)} />
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
