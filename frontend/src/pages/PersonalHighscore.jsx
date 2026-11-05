import React from 'react'
import { useParams } from 'react-router-dom'
import { Typography, Box } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import SubmissionCard from '../components/SubmissionCard'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import useGame from '../hooks/useGame'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'
import useAsyncData from '../hooks/useAsyncData'

export default function PersonalHighscore() {
  const { gameId } = useParams()
  const { user, loading: authLoading } = useRequireAuth()
  const { game, notFound, error, loading: gameLoading, refresh } = useGame(gameId)
  const userId = user?.id ?? null
  const hasGame = !!game
  const { data: highscoreData, error: highscoreError, refetch } = useAsyncData(
    () => (!authLoading && userId && hasGame)
      ? api.get(`/games/${gameId}/personal-highscore`).then(r => (r.data || {}).top || [])
      : Promise.resolve(null),
    [authLoading, userId, hasGame, gameId]
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
  if (authLoading || gameLoading || !game) return <Loading />

  return (
    <div>
      <Typography variant="h5">Your Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {highscoreError ? (
          <Box sx={{ py: 2 }}>
            <Typography gutterBottom>Failed to load your highscores.</Typography>
            <AppButton onClick={refetch}>Retry</AppButton>
          </Box>
        ) : top.length === 0 ? (
          <div className="muted">You have no submissions yet.</div>
        ) : (
          <SubmissionGrid items={top} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
        )}
      </div>
    </div>
  )
}
