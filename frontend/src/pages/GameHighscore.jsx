import React, { useEffect, useState, useRef } from 'react'
import { useParams, Link } from 'react-router-dom'
import { Typography } from '@mui/material'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import AppButton from '../components/ui/AppButton'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import SubmissionCard from '../components/SubmissionCard'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function GameHighscore() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
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

    // Lightweight check for whether the current user has submitted today.
    try {
      const sres = await api.get(`/submissions/game/${gameId}/has-submitted`)
      setHasSubmittedForLatest(sres.data?.hasSubmittedForLatest === true)
    } catch (e) {
      setHasSubmittedForLatest(false)
    }

    const res = await api.get(`/games/${gameId}/highscore`)
    const data = res.data || { top: [] }
    setTop(data.top || [])
  }

  if (notFound) return <NotFound message="Game not found" />
  if (!game) return <Loading />

  const currentScoringDay = game?.currentScoringDay ?? ''

  return (
    <div>
      <Typography variant="h5">Highscores — {game.name}</Typography>
      <div style={{ marginTop: 12 }}>
        {currentScoringDay && !hasSubmittedForLatest ? (
          <HiddenScoresCard gameId={game.id} />
        ) : (
          (top.length === 0) ? (
            <div className="muted">No highscores yet.</div>
          ) : (
            <SubmissionGrid items={top} ItemComponent={SubmissionCard} containerSx={{ mt: 1 }} />
          )
        )}
      </div>
    </div>
  )
}
