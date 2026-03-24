import React, { useEffect, useState, useRef } from 'react'
import { useParams, Link } from 'react-router-dom'
import { Typography, Grid, Button } from '@mui/material'
import SubmissionCard from '../components/SubmissionCard'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function GameHighscore() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [top, setTop] = useState([])
  const { user } = useAuth()
  const [hasSubmittedForLatest, setHasSubmittedForLatest] = useState(false)

  // Avoid duplicate fetches when React StrictMode or auth updates trigger
  // multiple mounts/updates. Use a fetch key so identical requests are skipped.
  const lastFetchKeyRef = useRef(null)
  useEffect(() => { fetchData() }, [gameId, user?.id])


  const fetchData = async () => {
    const fetchKey = `${gameId}-${user?.id ?? 'anon'}`
    if (lastFetchKeyRef.current === fetchKey) return
    lastFetchKeyRef.current = fetchKey

    const gres = await api.get('/games')
    const g = gres.data.find(x => String(x.id) === String(gameId))
    setGame(g)

    // Fetch submissions only to check whether the current user has submitted today.
    try {
      const sres = await api.get(`/submissions/game/${gameId}?page=1&pageSize=200`)
      const pageResult = sres.data || { items: [], hasSubmittedForLatest: false }
      setHasSubmittedForLatest(pageResult.hasSubmittedForLatest === true)
    } catch (e) {
      setHasSubmittedForLatest(false)
    }

    const res = await api.get(`/games/${gameId}/highscore`)
    const data = res.data || { top: [] }
    setTop(data.top || [])
  }

  if (!game) return <div>Loading...</div>

  const currentScoringDay = game?.currentScoringDay ?? ''

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {currentScoringDay && !hasSubmittedForLatest ? (
          <div className="card" style={{ padding: 24 }}>
            <Typography variant="h6">Today's scores are hidden.</Typography>
            <div className="muted" style={{ marginTop: 8 }}>Submit your score to view the leaderboard for today.</div>
            <div style={{ marginTop: 12 }}>
              <Button component={Link} to={`/submit/${game.id}`} className="btn">Submit Score</Button>
            </div>
          </div>
        ) : (
          (top.length === 0) ? (
            <div className="muted">No highscores yet.</div>
          ) : (
            <Grid container spacing={2} sx={{ mt: 1 }}>
              {top.map(s => (
                <Grid item xs={12} sm={6} md={4} key={s.id}>
                  <SubmissionCard submission={s} />
                </Grid>
              ))}
            </Grid>
          )
        )}
      </div>
    </div>
  )
}
