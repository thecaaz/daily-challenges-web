import React, { useEffect, useState } from 'react'
import { useParams, Link, useLocation } from 'react-router-dom'
import { Typography, Grid, Card, CardContent, CardMedia, Button, Stack, MenuItem, Select, FormControl, InputLabel } from '@mui/material'
import api from '../api'

export default function GameSubmissions() {
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
    // normalize date strings (yyyy-mm-dd)
    subs.forEach(s => s._date = (new Date(s.createdAt)).toISOString().split('T')[0])
    setSubmissions(subs)

    const dates = Array.from(new Set(subs.map(s => s._date))).sort().reverse()
    setAvailableDates(dates)
    // prefer date passed by navigation state
    const preferred = location?.state?.selectedDate
    setSelectedDate(preferred && dates.includes(preferred) ? preferred : (dates[0] ?? ''))
  }

  const filtered = selectedDate ? submissions.filter(s => s._date === selectedDate) : submissions

  if (!game) return <div>Loading...</div>

  const apiRoot = api.defaults.baseURL ? api.defaults.baseURL.replace(/\/api\/?$/, '') : 'http://localhost:5000'

  return (
    <div>
      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Typography variant="h5">Submissions — {game.name}</Typography>
        <div>
          <Button component={Link} to="/" sx={{ mr: 1 }}>Back</Button>
          <Button component={Link} to={`/submit/${game.id}`} variant="contained">Submit Score</Button>
        </div>
      </Stack>

      <FormControl sx={{ mb: 2, minWidth: 200 }}>
        <InputLabel id="date-select-label">Day</InputLabel>
        <Select labelId="date-select-label" value={selectedDate} label="Day" onChange={e => setSelectedDate(e.target.value)}>
          <MenuItem value="">All</MenuItem>
          {availableDates.map(d => <MenuItem key={d} value={d}>{d}</MenuItem>)}
        </Select>
      </FormControl>

      <Grid container spacing={2}>
        {filtered.map(s => (
          <Grid item xs={12} sm={6} md={4} key={s.id}>
            <Card>
              {s.screenshotUrl && <CardMedia component="img" height="200" image={`${apiRoot}${s.screenshotUrl}`} />}
              <CardContent>
                <Typography variant="subtitle1">{s.username ?? 'Anonymous'}</Typography>
                <Typography variant="body2">Score: {s.score}</Typography>
                <Typography variant="caption">{new Date(s.createdAt).toLocaleString()}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </div>
  )
}
