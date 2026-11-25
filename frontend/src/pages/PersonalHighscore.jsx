import React from 'react'
import { useParams } from 'react-router-dom'
import { Typography, Box } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import SubmissionCard from '../components/SubmissionCard'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import useRequireAuth from '../hooks/useRequireAuth'
import useGame from '../hooks/useGame'
import usePersonalHighscores from '../hooks/usePersonalHighscores'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'

function PersonalHighscoreResults({ highscores, error, onRetry }) {
  if (error) {
    return (
      <Box sx={{ py: 2 }}>
        <Typography gutterBottom>Failed to load your highscores.</Typography>
        <AppButton onClick={onRetry}>Retry</AppButton>
      </Box>
    )
  }
  if (highscores.length === 0) return <div className="muted">You have no submissions yet.</div>
  return <SubmissionGrid items={highscores} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
}

export default function PersonalHighscore() {
  const { gameId } = useParams()
  const { user, loading: authLoading } = useRequireAuth()
  const { game, notFound, error, loading: gameLoading, refresh } = useGame(gameId)
  const { highscores, error: highscoreError, retry } = usePersonalHighscores(gameId, {
    userId: user?.id ?? null,
    authLoading,
    hasGame: !!game,
  })

  if (notFound) return <NotFound message="Game not found" />
  if (!game && error) {
    return (
      <Box sx={{ textAlign: 'center', py: 6 }}>
        <Typography gutterBottom>Failed to load this game.</Typography>
        <AppButton onClick={refresh}>Retry</AppButton>
      </Box>
    )
  }
  if (authLoading || gameLoading || !game) return <Loading />

  return (
    <div>
      <Typography variant="h5">Your Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        <PersonalHighscoreResults highscores={highscores} error={highscoreError} onRetry={retry} />
      </div>
    </div>
  )
}
