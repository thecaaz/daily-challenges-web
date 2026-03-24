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
  const [page, setPage] = useState(1)
  const [pageSize] = useState(50)
  const [hasMore, setHasMore] = useState(true)
  const [selectedDate, setSelectedDate] = useState('')
  const [availableDates, setAvailableDates] = useState([])
  const [hasSubmittedForLatest, setHasSubmittedForLatest] = useState(false)

  const location = useLocation()

  useEffect(() => { fetchData() }, [])

  const fetchData = async () => {
    const gres = await api.get('/games')
    const g = gres.data.find(x => String(x.id) === String(gameId))
    setGame(g)

    const sres = await api.get(`/submissions/game/${gameId}?page=1&pageSize=${pageSize}`)
    const pageResult = sres.data || { items: [], hasSubmittedForLatest: false, hasMore: false }
    const subs = pageResult.items || []
    setSubmissions(subs)
    setPage(1)
    // Prefer server-provided paging info when available
    if (typeof pageResult.totalPages === 'number') setHasMore(pageResult.page < pageResult.totalPages)
    else setHasMore(pageResult.hasMore === true)

    const dates = pageResult.availableDates || []
    setAvailableDates(dates)
    setHasSubmittedForLatest(pageResult.hasSubmittedForLatest === true)
    // prefer date passed by navigation state
    const preferred = location?.state?.selectedDate
    // compute current scoring day (use server-provided when available)
    const currentDay = g?.currentScoringDay ?? ''
    // If the preferred date is supplied and available, use it. Otherwise,
    // only auto-select the current scoring day if it actually has submissions;
    // otherwise leave selection empty (show 'All').
    if (preferred && dates.includes(preferred)) setSelectedDate(preferred)
    else if (currentDay && dates.includes(currentDay)) setSelectedDate(currentDay)
    else setSelectedDate('')
  }

  const loadMore = async () => {
    const next = page + 1
    const res = await api.get(`/submissions/game/${gameId}?page=${next}&pageSize=${pageSize}`)
    const pageResult = res.data || { items: [], hasMore: false }
    const more = pageResult.items || []
    setSubmissions(prev => [...prev, ...more])
    setPage(next)
    if (typeof pageResult.totalPages === 'number') setHasMore(pageResult.page < pageResult.totalPages)
    else setHasMore(pageResult.hasMore === true)
  }


  const filtered = selectedDate ? submissions.filter(s => s.scoringDay === selectedDate) : submissions

  // Determine whether we're viewing the latest day (most recent scoring day)
  const currentScoringDay = game?.currentScoringDay ?? ''
  const isViewingLatest = !selectedDate || selectedDate === currentScoringDay

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
            <>
            <Grid container spacing={2}>
              {filtered.map(s => (
                <Grid item xs={12} sm={6} md={4} key={s.id}>
                  <Link to={`/submission/${s.id}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                    <div className="card">
                      {s.screenshotUrl && <img className="game-list-image" src={`${apiRoot}${s.screenshotUrl}`} alt="screenshot" />}
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
            {hasMore && (
              <div style={{ marginTop: 16, textAlign: 'center' }}>
                <Button onClick={loadMore} className="btn">Load more</Button>
              </div>
            )}
            </>
          )}
    </div>
  )
}
