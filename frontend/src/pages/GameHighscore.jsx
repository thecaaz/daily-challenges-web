import React from 'react'
import { useParams } from 'react-router-dom'
import { Typography, Box } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import SubmissionCard from '../components/SubmissionCard'
import api from '../api'
import useGame from '../hooks/useGame'
import useAsyncData from '../hooks/useAsyncData'

export default function GameHighscore() {
  const { gameId } = useParams()
  const { game, hasSubmittedForLatest, notFound, error, loading, refresh } = useGame(gameId)
  const hasGame = !!game
  // hasSubmittedForLatest is a dependency: submitting reveals today's entries.
  const { data: highscoreData, error: highscoreError, refetch } = useAsyncData(
    () => hasGame
      ? api.get(`/games/${gameId}/highscore`).then(r => (r.data || {}).top || [])
      : Promise.resolve(null),
    [hasGame, gameId, hasSubmittedForLatest]
  )
  const top = highscoreData ?? []

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
        {!hasSubmittedForLatest && (
          <HiddenScoresCard gameId={game.id} />
        )}

        {highscoreError ? (
          <Box sx={{ py: 2 }}>
            <Typography gutterBottom>Failed to load highscores.</Typography>
            <AppButton onClick={refetch}>Retry</AppButton>
          </Box>
        ) : top.length === 0 ? (
          <div className="muted">No highscores yet.</div>
        ) : (
          <SubmissionGrid items={top} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
        )}
      </div>
    </div>
  )
}
