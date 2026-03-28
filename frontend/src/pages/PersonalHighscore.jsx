import React, { useEffect, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { Typography, Grid } from '@mui/material'
import SubmissionCard from '../components/SubmissionCard'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function PersonalHighscore() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
  const [top, setTop] = useState([])
  const { user, loading } = useAuth()
  const navigate = useNavigate()

  useEffect(() => { fetchData() }, [loading, user])

  const fetchData = async () => {
    if (!loading && !user) {
      navigate('/login')
      return
    }
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

  if (notFound)
    return (
      <Typography variant="h5" role="alert">
        Game not found
      </Typography>
    )
  if (!game) return <div>Loading...</div>

  return (
    <div>
      <Typography variant="h5">Your Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {top.length === 0 ? (
          <div className="muted">You have no submissions yet.</div>
        ) : (
          <Grid container spacing={2} sx={{ mt: 1 }}>
            {top.map(s => (
              <Grid item xs={12} sm={6} md={4} key={s.id}>
                <SubmissionCard submission={s} />
              </Grid>
            ))}
          </Grid>
        )}
      </div>
    </div>
  )
}
