import React, { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Typography } from '@mui/material'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import SubmissionCard from '../components/SubmissionCard'
import api from '../api'
import useGame from '../hooks/useGame'

export default function GameHighscore() {
  const { gameId } = useParams()
  const [top, setTop] = useState([])
  const { game, hasSubmittedForLatest, notFound, loading } = useGame(gameId)

  useEffect(() => {
    if (!game) return
    api.get(`/games/${gameId}/highscore`).then(res => {
      const data = res.data || { top: [] }
      setTop(data.top || [])
    })
  }, [game, gameId])

  if (notFound) return <NotFound message="Game not found" />
  if (loading || !game) return <Loading />

  const currentScoringDay = game?.currentScoringDay ?? ''

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {currentScoringDay && !hasSubmittedForLatest ? (
          <HiddenScoresCard gameId={game.id} />
        ) : (
          (top.length === 0) ? (
            <div className="muted">No highscores yet.</div>
          ) : (
            <SubmissionGrid items={top} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
          )
        )}
      </div>
    </div>
  )
}
