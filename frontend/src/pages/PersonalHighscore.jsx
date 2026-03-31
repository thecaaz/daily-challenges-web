import React, { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { Typography } from '@mui/material'
import SubmissionCard from '../components/SubmissionCard'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'

export default function PersonalHighscore() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
  const [top, setTop] = useState([])
  const { user, loading } = useRequireAuth()

  useEffect(() => { fetchData() }, [loading, user])

  const fetchData = async () => {
    if (loading || !user) return
    try {
      const gres = await api.get(`/games/${gameId}`)
      setGame(gres.data)
    } catch (err) {
      if (err?.response?.status === 404) {
        setNotFound(true)
        return
      }
      throw err
    }
    const res = await api.get(`/games/${gameId}/personal-highscore`)
    const data = res.data || { top: [] }
    setTop(data.top || [])
  }

  if (notFound) return <NotFound message="Game not found" />
  if (!game) return <Loading />

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
