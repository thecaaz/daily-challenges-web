import React from 'react'
import { useParams } from 'react-router-dom'
import { Typography } from '@mui/material'
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
  const { game, hasSubmittedForLatest, notFound, loading } = useGame(gameId)
  const { data: highscoreData } = useAsyncData(
    () => game
      ? api.get(`/games/${gameId}/highscore`).then(r => (r.data || {}).top || [])
      : Promise.resolve(null),
    [game, gameId]
  )
  const top = highscoreData ?? []

  if (notFound) return <NotFound message="Game not found" />
  if (loading || !game) return <Loading />

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {!hasSubmittedForLatest && (
          <HiddenScoresCard gameId={game.id} />
        )}

        {top.length === 0 ? (
          <div className="muted">No highscores yet.</div>
        ) : (
          <SubmissionGrid items={top} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
        )}
      </div>
    </div>
  )
}
