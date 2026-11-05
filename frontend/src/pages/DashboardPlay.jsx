import React, { useCallback, useRef, useState } from 'react'
import { Alert, Box, CircularProgress, IconButton, Tooltip, Typography } from '@mui/material'
import FullscreenIcon from '@mui/icons-material/Fullscreen'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import SkipNextIcon from '@mui/icons-material/SkipNext'
import ExitToAppIcon from '@mui/icons-material/ExitToApp'
import { useNavigate } from 'react-router-dom'
import AppButton from '../components/ui/AppButton'
import useRequireAuth from '../hooks/useRequireAuth'
import usePlayableFavorites from '../hooks/usePlayableFavorites'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'
import { useScoreCapture } from '../hooks/useScoreCapture'
import GameIframePlayer from '../components/ui/GameIframePlayer'
import { isSafeHttpUrl } from '../utils/safeUrl'

export default function DashboardPlay() {
  const { user, loading: authLoading } = useRequireAuth()
  const navigate = useNavigate()

  // The queue is computed once per user; don't reshuffle it while playing.
  const favorites = usePlayableFavorites(user?.id, { enabled: !authLoading, subscribe: false })
  const queue = favorites.playable
  const [currentIndex, setCurrentIndex] = useState(0)

  const current = queue[currentIndex]
  const currentUrl = current && isSafeHttpUrl(current.url) ? current.url : null

  const iframeRef = useRef(null)
  const [isMaximized, setIsMaximized] = useState(false)
  const {
    score, setScore, capturedFile, setCapturedFile, showSubmission, setShowSubmission,
    submitScore, cancelCapture, canCapture, capturing, captureError,
  } = useScoreCapture(iframeRef, { isMaximized, url: currentUrl })

  // Leave the current game: abandon a capture still in flight (its late
  // responses are ignored) and drop any captured score/screenshot so it can't
  // be submitted for the next game.
  const advance = useCallback(() => {
    cancelCapture()
    setScore('')
    setCapturedFile(null)
    setShowSubmission(false)
    setCurrentIndex(i => i + 1)
  }, [cancelCapture, setScore, setCapturedFile, setShowSubmission])

  const cancelSubmission = useCallback(() => {
    setShowSubmission(false)
    setCapturedFile(null)
    setScore('')
  }, [setScore, setCapturedFile, setShowSubmission])

  if (authLoading || favorites.loading || favorites.checkingPlayable) {
    return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>
  }

  if (favorites.error) {
    return (
      <Box>
        <Typography variant="h5" sx={{ mb: 2 }}>Daily Player</Typography>
        <Alert severity="error" sx={{ mb: 2 }} action={<AppButton color="inherit" size="small" onClick={favorites.reload}>Retry</AppButton>}>
          Could not load your favorite games.
        </Alert>
        <AppButton to="/dashboard" variant="contained">Back to Dashboard</AppButton>
      </Box>
    )
  }

  if (queue.length === 0) {
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
          {currentUrl && (
            <Tooltip title="Open in new tab">
              <IconButton href={currentUrl} target="_blank" rel="noopener noreferrer" size="small" aria-label="Open in new tab">
                <OpenInNewIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
          {currentUrl && canCapture && !showSubmission && (
            <AppButton onClick={submitScore} variant="contained" size="small" disabled={capturing}>
              {capturing ? 'Capturing…' : 'Submit'}
            </AppButton>
          )}
          {currentUrl && !isMaximized && !showSubmission && (
            <Tooltip title="Maximize">
              <IconButton onClick={() => setIsMaximized(true)} size="small" aria-label="Maximize">
                <FullscreenIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
          <Tooltip title="Skip">
            <span>
              <IconButton size="small" onClick={advance} disabled={capturing} aria-label="Skip">
                <SkipNextIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title="Quit">
            <IconButton size="small" onClick={() => navigate('/dashboard')} aria-label="Quit">
              <ExitToAppIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      {showSubmission && captureError && (
        <Alert severity="warning" sx={{ mb: 2 }}>Automatic capture failed ({captureError}). Enter the score and add a screenshot manually.</Alert>
      )}

      {!currentUrl ? (
        <Typography>No playable URL for this game.</Typography>
      ) : !showSubmission ? (
        <GameIframePlayer
          key={current.id}
          src={currentUrl}
          title={current.name}
          iframeRef={iframeRef}
          isMaximized={isMaximized}
          onRestore={() => setIsMaximized(false)}
        />
      ) : (
        <SubmissionForm
          key={current.id}
          gameId={current.id}
          initialScore={score}
          initialScreenshot={capturedFile}
          hasSubmitted={current.hasSubmittedForLatest}
          onCancel={cancelSubmission}
          onSubmitted={advance}
        />
      )}
    </Box>
  )
}
