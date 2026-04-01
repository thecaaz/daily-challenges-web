import React, { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Typography } from '@mui/material'
import SubmissionCard from '../components/SubmissionCard'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import useGame from '../hooks/useGame'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'

export default function PersonalHighscore() {
  const { gameId } = useParams()
  const [top, setTop] = useState([])
  const { user, loading: authLoading } = useRequireAuth()
  const { game, notFound, loading: gameLoading } = useGame(gameId)

  useEffect(() => {
    if (authLoading || !user || !game) return
    api.get(`/games/${gameId}/personal-highscore`).then(res => {
      const data = res.data || { top: [] }
      setTop(data.top || [])
    })
  }, [authLoading, user, game, gameId])

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
