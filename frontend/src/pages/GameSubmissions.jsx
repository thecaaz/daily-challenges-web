import React, { useEffect, useState } from 'react'
import { useParams, Link, useLocation, useSearchParams } from 'react-router-dom'
import { Typography, Grid, Card, CardContent, CardMedia, Button, Stack, MenuItem, Select, FormControl, InputLabel } from '@mui/material'
import api from '../api'
import SubmissionCard from '../components/SubmissionCard'
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
  const [searchParams, setSearchParams] = useSearchParams()

  useEffect(() => { fetchData() }, [])

  // respond to browser back/forward changes to the scoringDay query param
  useEffect(() => {
    const paramDay = searchParams.get('scoringDay') || ''
    if (paramDay !== selectedDate) {
      // if param changed externally (history navigation), load that day
      handleDateChange(paramDay)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams])

  const buildSubmissionsUrl = (pageNum, scoringDay) => {
    let url = `/submissions/game/${gameId}?page=${pageNum}&pageSize=${pageSize}`
    if (scoringDay) url += `&scoringDay=${encodeURIComponent(scoringDay)}`
    return url
  }

  const fetchSubmissionsPage = async (pageNum, scoringDay) => {
    const res = await api.get(buildSubmissionsUrl(pageNum, scoringDay))
    return res.data || { items: [], hasSubmittedForLatest: false, hasMore: false }
  }

  const fetchData = async () => {
    const gres = await api.get('/games')
    const g = gres.data.find(x => String(x.id) === String(gameId))
    setGame(g)

    // Fetch canonical availableDates from dedicated endpoint, then fetch
    // an unfiltered submissions page to learn submission flags.
    let dates = []
    try {
      const dr = await api.get(`/submissions/game/${gameId}/available-dates`)
      dates = dr.data || []
    } catch (err) {
      dates = []
    }
    setAvailableDates(dates)

    const initial = await fetchSubmissionsPage(1)
    const initialSubs = initial.items || []
    setHasSubmittedForLatest(initial.hasSubmittedForLatest === true)
    if (typeof initial.totalPages === 'number') setHasMore(initial.page < initial.totalPages)
    else setHasMore(initial.hasMore === true)

    // prefer date passed by URL query param, then navigation state
    const preferred = searchParams.get('scoringDay') || location?.state?.selectedDate
    // compute current scoring day (use server-provided when available)
    const currentDay = g?.currentScoringDay ?? ''
    let initialSelected = ''
    if (preferred && dates.includes(preferred)) initialSelected = preferred
    else if (currentDay && dates.includes(currentDay)) initialSelected = currentDay
    else initialSelected = ''

    setSelectedDate(initialSelected)
    setPage(1)

    if (initialSelected) {
      const dayPage = await fetchSubmissionsPage(1, initialSelected)
      const subs = dayPage.items || []
      setSubmissions(subs)
      if (typeof dayPage.totalPages === 'number') setHasMore(dayPage.page < dayPage.totalPages)
      else setHasMore(dayPage.hasMore === true)
      // availableDates is provided by the dedicated endpoint; do not override here.
    } else {
      setSubmissions(initialSubs)
    }
  }

  const loadMore = async () => {
    const next = page + 1
    const pageResult = await fetchSubmissionsPage(next, selectedDate || undefined)
    const more = pageResult.items || []
    setSubmissions(prev => [...prev, ...more])
    setPage(next)
    if (typeof pageResult.totalPages === 'number') setHasMore(pageResult.page < pageResult.totalPages)
    else setHasMore(pageResult.hasMore === true)
  }

  const handleDateChange = async (value) => {
    // update url param
    if (value) setSearchParams({ scoringDay: value })
    else setSearchParams({})

    setSelectedDate(value)
    setPage(1)
    setSubmissions([])
    const pageResult = await fetchSubmissionsPage(1, value || undefined)
    const subs = pageResult.items || []
    setSubmissions(subs)
    if (typeof pageResult.totalPages === 'number') setHasMore(pageResult.page < pageResult.totalPages)
    else setHasMore(pageResult.hasMore === true)
    // availableDates is provided by the dedicated endpoint; do not override here.
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
            <Link to={`/games/${game.id}/highscore`}>Highscores</Link>
            <Link to={`/games/${game.id}/personal-highscore`}>Your Highscores</Link>
          </div>
          <div className="muted">Compete on daily challenges — climb the leaderboard!</div>
        </div>
        <div>
          <Button component={Link} to="/" className="btn" sx={{ mr: 1, background: 'white', color: '#444', boxShadow: 'none' }}>Back</Button>
          {(() => {
            const submitDisabled = isViewingLatest && hasSubmittedForLatest
              return (
              <Button
                component={submitDisabled ? 'span' : Link}
                to={submitDisabled ? undefined : `/submit/${game.id}${location.search || ''}`}
                className="btn"
                disabled={submitDisabled}
                title={submitDisabled ? "You've already submitted for today" : undefined}
              >
                Submit Score
              </Button>
            )
          })()}
        </div>
      </Stack>

      <FormControl sx={{ mb: 2, minWidth: 200 }}>
        <InputLabel id="date-select-label">Day</InputLabel>
        <Select labelId="date-select-label" value={selectedDate} label="Day" onChange={e => handleDateChange(e.target.value)}>
          <MenuItem value="">All</MenuItem>
          {availableDates.map(d => <MenuItem key={d} value={d}>{d}</MenuItem>)}
        </Select>
      </FormControl>

          {isViewingLatest && !hasSubmittedForLatest ? (
            <div className="card" style={{ padding: 24 }}>
              <Typography variant="h6">Today's scores are hidden.</Typography>
              <div className="muted" style={{ marginTop: 8 }}>Submit your score to view the leaderboard for today.</div>
              <div style={{ marginTop: 12 }}>
                <Button component={Link} to={`/submit/${game.id}${location.search || ''}`} className="btn">Submit Score</Button>
              </div>
            </div>
          ) : (
            <>
            <Grid container spacing={2}>
              {filtered.map(s => (
                <Grid item xs={12} sm={6} md={4} key={s.id}>
                  <SubmissionCard submission={s} />
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
