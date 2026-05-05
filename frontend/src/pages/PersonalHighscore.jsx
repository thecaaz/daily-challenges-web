import React from 'react'
import { useParams } from 'react-router-dom'
import { Typography } from '@mui/material'
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
  const { game, notFound, loading: gameLoading } = useGame(gameId)
  const { data: highscoreData } = useAsyncData(
    () => (!authLoading && user && game)
      ? api.get(`/games/${gameId}/personal-highscore`).then(r => (r.data || {}).top || [])
      : Promise.resolve(null),
    [authLoading, user, game, gameId]
  )
  const top = highscoreData ?? []

  if (notFound) return <NotFound message="Game not found" />
  if (authLoading || gameLoading || !game) return <Loading />

  return (
    <div>
      <Typography variant="h5">Your Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {top.length === 0 ? (
          <div className="muted">You have no submissions yet.</div>
        ) : (
          <SubmissionGrid items={top} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
        )}
      </div>
    </div>
  )
}
