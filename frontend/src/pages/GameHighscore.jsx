import React, { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Typography, Grid, CardContent, Button } from '@mui/material'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function GameHighscore() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [top, setTop] = useState([])
  const { user } = useAuth()
  const [hasSubmittedForLatest, setHasSubmittedForLatest] = useState(false)
  const [latestDate, setLatestDate] = useState('')

  useEffect(() => { fetchData() }, [user])

  const fetchData = async () => {
    const gres = await api.get('/games')
    const g = gres.data.find(x => String(x.id) === String(gameId))
    setGame(g)

    // fetch submissions to determine latest scoring day and whether user submitted
    try {
      const sres = await api.get(`/submissions/game/${gameId}`)
      const subs = sres.data || []
      const resetTime = g?.resetTime ?? '00:00'
      const tz = g?.resetTimezoneId ?? 'UTC'
      const [rh, rm] = (resetTime || '00:00').split(':').map(x => parseInt(x, 10) || 0)
      const resetMinutes = (rh * 60) + rm
      subs.forEach(s => {
        const dt = new Date(s.createdAt)
        const localDateStr = dt.toLocaleDateString('en-CA', { timeZone: tz }) // YYYY-MM-DD
        const timeParts = dt.toLocaleTimeString('en-GB', { hour12: false, timeZone: tz }).split(':')
        const localMinutes = (parseInt(timeParts[0] || '0', 10) * 60) + (parseInt(timeParts[1] || '0', 10))
        if (localMinutes < resetMinutes) {
          const [y, m, d] = localDateStr.split('-').map(x => parseInt(x, 10))
          const base = new Date(Date.UTC(y, (m - 1), d))
          base.setUTCDate(base.getUTCDate() - 1)
          s._date = base.toISOString().split('T')[0]
        } else {
          s._date = localDateStr
        }
      })
      const dates = Array.from(new Set(subs.map(s => s._date))).sort().reverse()
      const latest = dates[0] ?? ''
      setLatestDate(latest)
      if (latest && user && user.id) {
        const my = subs.find(s => s.userId === user.id && s._date === latest)
        setHasSubmittedForLatest(!!my)
      } else {
        setHasSubmittedForLatest(false)
      }
    } catch (e) {
      setHasSubmittedForLatest(false)
      setLatestDate('')
    }

    const res = await api.get(`/games/${gameId}/highscore`)
    const data = res.data || { top: [] }
    setTop(data.top || [])
  }

  if (!game) return <div>Loading...</div>

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {latestDate && !hasSubmittedForLatest ? (
          <div className="card" style={{ padding: 24 }}>
            <Typography variant="h6">Today's scores are hidden.</Typography>
            <div className="muted" style={{ marginTop: 8 }}>Submit your score to view the leaderboard for today.</div>
            <div style={{ marginTop: 12 }}>
              <Button href={`/submit/${game.id}`} className="btn">Submit Score</Button>
            </div>
          </div>
        ) : (
          (top.length === 0) ? (
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
          )
        )}
      </div>
    </div>
  )
}
