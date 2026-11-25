import React, { useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Alert, Box, CircularProgress, Typography, IconButton, Tooltip } from '@mui/material'
import FullscreenIcon from '@mui/icons-material/Fullscreen'
import AppButton from '../components/ui/AppButton'
import NotFound from '../components/ui/NotFound'
import useRequireAuth from '../hooks/useRequireAuth'
import useGame from '../hooks/useGame'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'
import { useScoreCapture } from '../hooks/useScoreCapture'
import GameIframePlayer from '../components/ui/GameIframePlayer'
import { isSafeHttpUrl } from '../utils/safeUrl'

export default function Play() {
  const { gameId } = useParams()
  useRequireAuth()

  const { game, hasSubmittedForLatest, notFound, error, loading, refresh } = useGame(gameId)
  const url = game && isSafeHttpUrl(game.url) ? game.url : null

  const iframeRef = useRef(null)
  const [isMaximized, setIsMaximized] = useState(false)
  const {
    score, setScore, capturedFile, setCapturedFile, showSubmission, setShowSubmission,
    submitScore, canCapture, capturing, captureError,
  } = useScoreCapture(iframeRef, { isMaximized, url })

  const cancelSubmission = () => {
    setShowSubmission(false)
    setCapturedFile(null)
    setScore('')
  }

  if (notFound) return <NotFound message="Game not found." />

  if (error && !game) {
    return (
      <Alert severity="error" sx={{ mt: 4 }} action={<AppButton color="inherit" size="small" onClick={refresh}>Retry</AppButton>}>
        Could not load this game.
      </Alert>
    )
  }

  if (loading && !game) return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>
  if (!game) return null

  const isPlaying = Boolean(url) && !showSubmission
  const isSubmitting = Boolean(url) && showSubmission
  const canSubmit = canCapture && !showSubmission
  const canMaximize = !isMaximized && !showSubmission
  const hasCaptureFailed = showSubmission && Boolean(captureError)

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h5">{game.name}</Typography>
        {url && (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <AppButton href={url} target="_blank" rel="noopener noreferrer" variant="outlined">Open in new tab</AppButton>
            {canSubmit && (
              <AppButton onClick={submitScore} variant="contained" disabled={capturing}>
                {capturing ? 'Capturing…' : 'Submit score'}
              </AppButton>
            )}
            {canMaximize && (
              <Tooltip title="Maximize">
                <IconButton onClick={() => setIsMaximized(true)} size="small" aria-label="Maximize">
                  <FullscreenIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
          </Box>
        )}
      </Box>

      {hasCaptureFailed && (
        <Alert severity="warning" sx={{ mb: 2 }}>Automatic capture failed ({captureError}). Enter the score and add a screenshot manually.</Alert>
      )}

      {!url && <Typography>No playable URL for this game.</Typography>}
      {isPlaying && (
        <GameIframePlayer src={url} title={game.name} iframeRef={iframeRef} isMaximized={isMaximized} onRestore={() => setIsMaximized(false)} />
      )}
      {isSubmitting && (
        <SubmissionForm
          gameId={gameId}
          initialScore={score}
          initialScreenshot={capturedFile}
          hasSubmitted={hasSubmittedForLatest}
          onCancel={cancelSubmission}
        />
      )}
    </Box>
  )
}
