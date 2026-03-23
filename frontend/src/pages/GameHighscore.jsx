import React, { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Typography, Grid, CardContent, Button } from '@mui/material'
import api from '../api'

export default function GameHighscore() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [top, setTop] = useState([])

  useEffect(() => { fetchData() }, [])

  const fetchData = async () => {
    const gres = await api.get('/games')
    const g = gres.data.find(x => String(x.id) === String(gameId))
    setGame(g)
    const res = await api.get(`/games/${gameId}/highscore`)
    const data = res.data || { top: [] }
    setTop(data.top || [])
  }

  if (!game) return <div>Loading...</div>

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {top.length === 0 ? (
          <div className="muted">No highscores yet.</div>
        ) : (
          <Grid container spacing={2} sx={{ mt: 1 }}>
            {top.map(s => (
              <Grid item xs={12} sm={6} md={4} key={s.id}>
                <div className="card">
                  <CardContent>
                    <Typography variant="subtitle1">{s.username ?? 'Anonymous'}</Typography>
                    <Typography variant="h6">{s.score}</Typography>
                    <Typography variant="caption">{new Date(s.createdAt).toLocaleString()}</Typography>
                  </CardContent>
                </div>
              </Grid>
            ))}
          </Grid>
        )}
      </div>
    </div>
  )
}
