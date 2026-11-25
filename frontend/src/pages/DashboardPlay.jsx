import React from 'react'
import { Alert, Box, CircularProgress, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import useRequireAuth from '../hooks/useRequireAuth'
import useFavoritesPlayer from '../hooks/useFavoritesPlayer'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'
import GameIframePlayer from '../components/ui/GameIframePlayer'
import PlayerToolbar from '../components/dashboard/PlayerToolbar'

function PlayerNotice({ title, children }) {
  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 2 }}>{title}</Typography>
      {children}
      <AppButton to="/dashboard" variant="contained">Back to Dashboard</AppButton>
    </Box>
  )
}

function CurrentGame({ player }) {
  const { current, currentUrl, iframeRef, isMaximized, setIsMaximized, advance, cancelSubmission } = player
  const { showSubmission, score, capturedFile } = player.capture

  if (!currentUrl) return <Typography>No playable URL for this game.</Typography>
  if (showSubmission) {
    return (
      <SubmissionForm
        key={current.id}
        gameId={current.id}
        initialScore={score}
        initialScreenshot={capturedFile}
        hasSubmitted={current.hasSubmittedForLatest}
        onCancel={cancelSubmission}
        onSubmitted={advance}
      />
    )
  }
  return (
    <GameIframePlayer
      key={current.id}
      src={currentUrl}
      title={current.name}
      iframeRef={iframeRef}
      isMaximized={isMaximized}
      onRestore={() => setIsMaximized(false)}
    />
  )
}

export default function DashboardPlay() {
  const { user, loading: authLoading } = useRequireAuth()
  const player = useFavoritesPlayer(user?.id, { enabled: !authLoading })
  const { favorites, queue, isFinished } = player
  const { showSubmission, captureError } = player.capture

  if (authLoading || favorites.loading || favorites.checkingPlayable) {
    return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>
  }

  if (favorites.error) {
    return (
      <PlayerNotice title="Daily Player">
        <Alert severity="error" sx={{ mb: 2 }} action={<AppButton color="inherit" size="small" onClick={favorites.reload}>Retry</AppButton>}>
          Could not load your favorite games.
        </Alert>
      </PlayerNotice>
    )
  }

  if (queue.length === 0) {
    return (
      <PlayerNotice title="Daily Player">
        <Typography sx={{ mb: 2 }} color="text.secondary">No playable favorite games found. Make sure you have favorites and the browser extension supports the game.</Typography>
      </PlayerNotice>
    )
  }

  if (isFinished) {
    return (
      <PlayerNotice title="All done">
        <Typography sx={{ mb: 2 }} color="text.secondary">You have finished the available playable favorites for today.</Typography>
      </PlayerNotice>
    )
  }

  const hasCaptureFailed = showSubmission && Boolean(captureError)

  return (
    <Box>
      <PlayerToolbar player={player} />
      {hasCaptureFailed && (
        <Alert severity="warning" sx={{ mb: 2 }}>Automatic capture failed ({captureError}). Enter the score and add a screenshot manually.</Alert>
      )}
      <CurrentGame player={player} />
    </Box>
  )
}
