import React, { useEffect, useState, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Box, CircularProgress, Typography, IconButton, Tooltip } from '@mui/material'
import FullscreenIcon from '@mui/icons-material/Fullscreen'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'
import { useScoreCapture } from '../hooks/useScoreCapture'
import GameIframePlayer from '../components/ui/GameIframePlayer'

export default function Play() {
  const { gameId } = useParams()
  const navigate = useNavigate()
  const { user, loading: authLoading, fetchMe } = useRequireAuth()

  const [game, setGame] = useState(null)
  const [loading, setLoading] = useState(true)

  const iframeRef = useRef(null)
  const [isMaximized, setIsMaximized] = useState(false)
  const { score, setScore, capturedFile, setCapturedFile, showSubmission, setShowSubmission, submitScore } = useScoreCapture(iframeRef, { isMaximized })

  // message handling moved to useScoreCapture hook

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

  // showSubmission handled in hook

  if (loading) return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>

  if (!game) return <Typography sx={{ mt: 4 }}>Game not found.</Typography>

  const url = game.url

  // submitScore provided by the hook


  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h5">{game.name}</Typography>
        {url && (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <AppButton href={url} target="_blank" rel="noreferrer" variant="outlined">Open in new tab</AppButton>
            <AppButton onClick={submitScore} variant="contained">Submit score</AppButton>
            {!isMaximized && (
              <Tooltip title="Maximize">
                <IconButton onClick={() => setIsMaximized(true)} size="small">
                  <FullscreenIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
          </Box>
        )}
      </Box>

      {!url ? (
        <Typography>No playable URL for this game.</Typography>
      ) : (
        <>
          {!showSubmission ? (
            <GameIframePlayer src={url} title={game.name} iframeRef={iframeRef} isMaximized={isMaximized} onRestore={() => setIsMaximized(false)} />
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
