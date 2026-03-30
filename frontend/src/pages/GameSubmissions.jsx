import React, { useEffect, useState } from 'react'
import { useParams, Link, useLocation, useSearchParams } from 'react-router-dom'
import { Typography, Grid, Card, CardContent, CardMedia, Button, Stack, MenuItem, Select, FormControl, InputLabel } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import SubmissionCard from '../components/SubmissionCard'
import { useAuth } from '../contexts/AuthContext'

export default function GameSubmissions() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
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
    // Prefer aggregated overview endpoint; fall back to parallel calls if unavailable
    try {
      const overviewRes = await api.get(`/games/${gameId}/overview?include=availableDates,hasSubmitted`)
      const overview = overviewRes.data || {}

      const gameData = overview.game
      if (!gameData) {
        // If the overview response didn't include game, fall back to old behavior
        throw new Error('Game overview endpoint returned incomplete data: missing "game" object')
      }

      setGame(gameData)

      const dates = overview.availableDates || []
      setAvailableDates(dates)

      const preferred = searchParams.get('scoringDay') || location?.state?.selectedDate
      const currentDay = gameData?.currentScoringDay ?? ''
      let initialSelected = ''
      if (preferred && dates.includes(preferred)) initialSelected = preferred
      else if (currentDay && dates.includes(currentDay)) initialSelected = currentDay
      else initialSelected = ''

      setSelectedDate(initialSelected)
      setPage(1)

      const userHasSubmitted = !!overview.hasSubmittedForLatest
      setHasSubmittedForLatest(userHasSubmitted)

      if (initialSelected) {
        const dayPage = await fetchSubmissionsPage(1, initialSelected)
        const subs = dayPage.items || []
        setSubmissions(subs)
        if (typeof dayPage.totalPages === 'number') setHasMore(dayPage.page < dayPage.totalPages)
        else setHasMore(dayPage.hasMore === true)
      } else {
        // If the user has not submitted for the latest scoring day and has not selected a day,
        // skip fetching the submissions list to avoid loading today's hidden submissions.
        if (!userHasSubmitted) {
          setSubmissions([])
          setHasMore(false)
        } else {
          const initial = await fetchSubmissionsPage(1)
          const initialSubs = initial.items || []
          if (typeof initial.totalPages === 'number') setHasMore(initial.page < initial.totalPages)
          else setHasMore(initial.hasMore === true)
          setSubmissions(initialSubs)
        }
      }
    } catch (err) {
      const e = err
      if (e?.response?.status === 404) {
        setNotFound(true)
        return
      }
      throw err
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
    // If clearing selection (viewing latest) and the user hasn't submitted, skip fetching list
    if (!value && !hasSubmittedForLatest) {
      setHasMore(false)
      return
    }

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

  if (notFound) return <div role="alert">Game not found</div>
  if (!game) return <div>Loading...</div>

  return (
    <div>
      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <div>
          <Typography variant="h5">Submissions — {game.name}</Typography>
          {game.url && (
            <div style={{ marginTop: 6 }}>
              <AppButton
                href={game.url}
                target="_blank"
                rel="noopener noreferrer"
                variant="text"
                size="small"
                sx={{ p: 0, minWidth: 'auto', textTransform: 'none' }}
                dataTest="game-play-link"
              >
                Play
              </AppButton>
            </div>
          )}
          <div style={{ marginTop: 6, display: 'flex', gap: 8 }}>
            <AppButton
              to={`/games/${game.id}/highscore`}
              variant="text"
              size="small"
              sx={{ p: 0, minWidth: 'auto', textTransform: 'none' }}
              dataTest="game-highscores-link"
            >
              Highscores
            </AppButton>
            <AppButton
              to={`/games/${game.id}/personal-highscore`}
              variant="text"
              size="small"
              sx={{ p: 0, minWidth: 'auto', textTransform: 'none' }}
              dataTest="game-personal-highscores-link"
            >
              Your Highscores
            </AppButton>
          </div>
          <div className="muted">Compete on daily challenges — climb the leaderboard!</div>
        </div>
        <div>
          <AppButton to="/" sx={{ mr: 1, background: 'white', color: '#444', boxShadow: 'none' }}>Back</AppButton>
          {(() => {
            const submitDisabled = isViewingLatest && hasSubmittedForLatest
              return (
              <AppButton
                component={submitDisabled ? 'span' : undefined}
                to={submitDisabled ? undefined : `/submit/${game.id}${location.search || ''}`}
                disabled={submitDisabled}
                title={submitDisabled ? "You've already submitted for today" : undefined}
              >
                Submit Score
              </AppButton>
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
                <AppButton to={`/submit/${game.id}${location.search || ''}`}>Submit Score</AppButton>
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
                <AppButton onClick={loadMore}>Load more</AppButton>
              </div>
            )}
            </>
          )}
    </div>
  )
}
