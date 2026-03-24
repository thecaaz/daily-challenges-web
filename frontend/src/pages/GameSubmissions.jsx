import React, { useEffect, useState } from 'react'
import { useParams, Link, useLocation } from 'react-router-dom'
import { Typography, Grid, Card, CardContent, CardMedia, Button, Stack, MenuItem, Select, FormControl, InputLabel } from '@mui/material'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function GameSubmissions() {
  const { user } = useAuth()
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [submissions, setSubmissions] = useState([])
  const [selectedDate, setSelectedDate] = useState('')
  const [availableDates, setAvailableDates] = useState([])

  const location = useLocation()

  useEffect(() => { fetchData() }, [])

  const fetchData = async () => {
    const gres = await api.get('/games')
    const g = gres.data.find(x => String(x.id) === String(gameId))
    setGame(g)

    const sres = await api.get(`/submissions/game/${gameId}`)
    const subs = sres.data || []
    // normalize date strings according to game's reset time/timezone
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
        // subtract one day from localDateStr
        const [y, m, d] = localDateStr.split('-').map(x => parseInt(x, 10))
        const base = new Date(Date.UTC(y, (m - 1), d))
        base.setUTCDate(base.getUTCDate() - 1)
        s._date = base.toISOString().split('T')[0]
      } else {
        s._date = localDateStr
      }
    })
    setSubmissions(subs)

    const dates = Array.from(new Set(subs.map(s => s._date))).sort().reverse()
    setAvailableDates(dates)
    // prefer date passed by navigation state
    const preferred = location?.state?.selectedDate
    // compute current scoring day (use server-provided when available)
    const currentDay = computeCurrentScoringDay(g)
    // If the preferred date is supplied and available, use it. Otherwise,
    // only auto-select the current scoring day if it actually has submissions;
    // otherwise leave selection empty (show 'All').
    if (preferred && dates.includes(preferred)) setSelectedDate(preferred)
    else if (currentDay && dates.includes(currentDay)) setSelectedDate(currentDay)
    else setSelectedDate('')
  }

  // Compute the current scoring day. Prefer server-provided value when available
  // (authoritative, avoids TZ format mismatches). Fallback to client-side clock calculation.
  const computeCurrentScoringDay = (g) => {
    if (!g) return ''
    if (g.currentScoringDay) return g.currentScoringDay
    const tz = g.resetTimezoneId ?? 'UTC'
    const [rh, rm] = (g.resetTime ?? '00:00').split(':').map(x => parseInt(x, 10) || 0)
    const resetMinutes = rh * 60 + rm
    const now = new Date()
    const localDateStr = now.toLocaleDateString('en-CA', { timeZone: tz })
    const timeParts = now.toLocaleTimeString('en-GB', { hour12: false, timeZone: tz }).split(':')
    const localMinutes = parseInt(timeParts[0] || '0', 10) * 60 + parseInt(timeParts[1] || '0', 10)
    if (localMinutes < resetMinutes) {
      const [y, m, d] = localDateStr.split('-').map(x => parseInt(x, 10))
      const base = new Date(Date.UTC(y, m - 1, d))
      base.setUTCDate(base.getUTCDate() - 1)
      return base.toISOString().split('T')[0]
    }
    return localDateStr
  }

  const filtered = selectedDate ? submissions.filter(s => s._date === selectedDate) : submissions

  // Determine whether we're viewing the latest day (most recent scoring day)
  const currentScoringDay = computeCurrentScoringDay(game)
  const isViewingLatest = !selectedDate || selectedDate === currentScoringDay
  const hasSubmittedForLatest = (() => {
    if (!currentScoringDay) return false
    if (!submissions || submissions.length === 0) return false
    if (!user || !user.id) return false
    return submissions.some(s => s._date === currentScoringDay && s.userId === user.id)
  })()

  if (!game) return <div>Loading...</div>

  const apiRoot = api.defaults.baseURL ? api.defaults.baseURL.replace(/\/api\/?$/, '') : 'http://localhost:5000'

  return (
    <div>
      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <div>
          <Typography variant="h5">Submissions — {game.name}</Typography>
          {game.url && (
            <div style={{ marginTop: 6 }}>
              <a href={game.url} target="_blank" rel="noreferrer">Play</a>
            </div>
          )}
          <div style={{ marginTop: 6, display: 'flex', gap: 8 }}>
            <a href={`/games/${game.id}/highscore`}>Highscores</a>
            <a href={`/games/${game.id}/personal-highscore`}>Your Highscores</a>
          </div>
          <div className="muted">Compete on daily challenges — climb the leaderboard!</div>
        </div>
        <div>
          <Button component={Link} to="/" className="btn" sx={{ mr: 1, background: 'white', color: '#444', boxShadow: 'none' }}>Back</Button>
          <Button component={Link} to={`/submit/${game.id}`} className="btn">Submit Score</Button>
        </div>
      </Stack>

      <FormControl sx={{ mb: 2, minWidth: 200 }}>
        <InputLabel id="date-select-label">Day</InputLabel>
        <Select labelId="date-select-label" value={selectedDate} label="Day" onChange={e => setSelectedDate(e.target.value)}>
          <MenuItem value="">All</MenuItem>
          {availableDates.map(d => <MenuItem key={d} value={d}>{d}</MenuItem>)}
        </Select>
      </FormControl>

          {isViewingLatest && !hasSubmittedForLatest ? (
            <div className="card" style={{ padding: 24 }}>
              <Typography variant="h6">Today's scores are hidden.</Typography>
              <div className="muted" style={{ marginTop: 8 }}>Submit your score to view the leaderboard for today.</div>
              <div style={{ marginTop: 12 }}>
                <Button component={Link} to={`/submit/${game.id}`} className="btn">Submit Score</Button>
              </div>
            </div>
          ) : (
            <Grid container spacing={2}>
              {filtered.map(s => (
                <Grid item xs={12} sm={6} md={4} key={s.id}>
                  <Link to={`/submission/${s.id}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                    <div className="card">
                      {s.screenshotUrl && <img className="game-image" src={`${apiRoot}${s.screenshotUrl}`} alt="screenshot" />}
                      <CardContent>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
                            <Typography variant="subtitle1">{s.username ?? 'Anonymous'}</Typography>
                            {s.userId && user && s.userId === user.id && (
                              <Typography variant="caption" sx={{ color: '#666' }}>You</Typography>
                            )}
                          </div>
                          <div className="badge">#{s.rank ?? ''}</div>
                        </div>
                        <Typography variant="h6">{s.score}</Typography>
                        <Typography variant="caption">{new Date(s.createdAt).toLocaleString()}</Typography>
                      </CardContent>
                    </div>
                  </Link>
                </Grid>
              ))}
            </Grid>
          )}
    </div>
  )
}
