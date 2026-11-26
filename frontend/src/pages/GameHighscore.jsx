import React from 'react'
import { useParams } from 'react-router-dom'
import { Typography, Box } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import useGame from '../hooks/useGame'
import useGameHighscores from '../hooks/useGameHighscores'

function HighscoreResults({ highscores, error, onRetry }) {
  if (error) {
    return (
      <Box sx={{ py: 2 }}>
        <Typography gutterBottom>Failed to load highscores.</Typography>
        <AppButton onClick={onRetry}>Retry</AppButton>
      </Box>
    )
  }
  if (highscores.length === 0) return <div className="muted">No highscores yet.</div>
  return <SubmissionGrid submissions={highscores} />
}

export default function GameHighscore() {
  const { gameId } = useParams()
  const { game, hasSubmittedForLatest, notFound, error, loading, refresh } = useGame(gameId)
  const { highscores, error: highscoreError, retry } = useGameHighscores(gameId, { hasGame: !!game, hasSubmittedForLatest })

  if (notFound) return <NotFound message="Game not found" />
  if (!game && error) {
    return (
      <Box sx={{ textAlign: 'center', py: 6 }}>
        <Typography gutterBottom>Failed to load this game.</Typography>
        <AppButton onClick={refresh}>Retry</AppButton>
      </Box>
    )
  }
  if (loading || !game) return <Loading />

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {!hasSubmittedForLatest && <HiddenScoresCard gameId={game.id} />}
        <HighscoreResults highscores={highscores} error={highscoreError} onRetry={retry} />
      </div>
    </div>
  )
}
