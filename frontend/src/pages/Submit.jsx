import React, { useState, useEffect } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { TextField, Button, Stack, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import parseUtcDate from '../utils/parseUtcDate'
import { useSnackbar } from '../contexts/SnackbarContext'
import { useAuth } from '../contexts/AuthContext'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'
import useGame from '../hooks/useGame'
import useImageUpload from '../hooks/useImageUpload'
import ImagePreview from '../components/ui/ImagePreview'

export default function Submit() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
  const [username, setUsername] = useState('')
  const [score, setScore] = useState('')
  const { showSnackbar } = useSnackbar()

  const { screenshot, previewUrl, setScreenshot, onFileChange, clear } = useImageUpload(showSnackbar)

  // Use centralized hook to load game overview
  const { game: hookGame, hasSubmittedForLatest: hookHasSubmittedForLatest, notFound: hookNotFound } = useGame(gameId)

  useEffect(() => {
    if (hookNotFound) {
      setNotFound(true)
      return
    }
    if (hookGame) setGame(hookGame)
  }, [hookGame, hookNotFound])
  const { user, loading, fetchMe } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    if (!loading && !user) {
      navigate('/login')
    }
  }, [loading, user, navigate])
  const [hasSubmitted, setHasSubmitted] = useState(false)

  // sync has-submitted state for authenticated users
  useEffect(() => {
    if (hookHasSubmittedForLatest && user && user.id) {
      setHasSubmitted(true)
    }
  }, [hookHasSubmittedForLatest, user])

  const submit = async (e) => {
    e.preventDefault()
    if (user && user.id && hasSubmitted) {
      showSnackbar('You have already submitted for this game', 'error')
      return
    }

    const fd = new FormData()
    fd.append('gameId', gameId)
    fd.append('score', score)
    // if authenticated, server will use identity (do not allow spoofing)
    if (!user || !user.id) {
      if (username) fd.append('username', username)
    }
    if (screenshot) fd.append('screenshot', screenshot)
    try {
      const res = await api.post('/submissions', fd)
      const { submission, xpGain } = res.data

      // Capture current level before refreshing, so we can detect a level-up.
      const prevLevel = user?.level ?? 1

      // Refresh user context so GameBar reflects the new XP/level immediately.
      const updatedUser = await fetchMe()

      const leveledUp = updatedUser && updatedUser.level > prevLevel

      if (leveledUp) {
        // Re-fetch once more to ensure xpIntoLevel / xpToNextLevel reflect the new
        // level's range after the level-up boundary was crossed.
        await fetchMe()
        showSnackbar(
          xpGain > 0
            ? `Submitted! +${xpGain} XP ⬆️ Level ${updatedUser.level}!`
            : `Level ${updatedUser.level}!`,
          'success'
        )
      } else if (xpGain > 0) {
        showSnackbar(`Submitted! +${xpGain} XP`, 'success')
      } else {
        showSnackbar('Submitted!', 'success')
      }

      // Navigate to the game's submissions filtered by the new submission's scoring day.
      const date = submission?.createdAt
        ? parseUtcDate(submission.createdAt).toISOString().split('T')[0]
        : new Date().toISOString().split('T')[0]
      navigate(`/games/${gameId}?scoringDay=${date}`)
    } catch (err) {
      // Prefer backend-provided message when available
      const msg = err?.response?.data?.message || err?.response?.data || err?.message || 'Failed to submit'
      showSnackbar(String(msg), 'error')
    }
  }

  // image upload and paste handling managed by useImageUpload(showSnackbar)

  if (notFound) return <NotFound message="Game not found" />
  if (!game) return <Loading />

  return (
    <div>
      <div className="card">
        <Typography variant="h5">Submit for {game.name}</Typography>
        {game.url && (
          <div style={{ marginTop: 6 }}>
            <a href={game.url} target="_blank" rel="noreferrer">Play</a>
          </div>
        )}
        <form onSubmit={submit}>
          <Stack spacing={2} maxWidth={480} sx={{ mt: 2 }}>
            {(!user || !user.id) && (
              <TextField label="Username (optional)" value={username} onChange={e => setUsername(e.target.value)} />
            )}
            <TextField label="Score" value={score} onChange={e => setScore(e.target.value)} required />
            <input type="file" accept="image/*" onChange={onFileChange} />
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <div style={{ fontSize: 12, color: '#666' }}>You can paste an image from clipboard (Ctrl+V).</div>
            </div>
            {previewUrl && (
              <ImagePreview previewUrl={previewUrl} onRemove={clear} />
            )}
            <AppButton type="submit">Submit</AppButton>
          </Stack>
        </form>
      </div>
      
    </div>
  )
}
