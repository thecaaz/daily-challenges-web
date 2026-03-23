import React, { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { TextField, Button, Stack, Typography } from '@mui/material'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { useAuth } from '../contexts/AuthContext'

export default function Submit() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [username, setUsername] = useState('')
  const [score, setScore] = useState('')
  const [screenshot, setScreenshot] = useState(null)
  const { showSnackbar } = useSnackbar()

  const [previewUrl, setPreviewUrl] = useState(null)

  useEffect(() => { fetchGame() }, [])
  const { user, loading } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (!loading && !user) {
      navigate('/login')
    }
  }, [loading, user, navigate])
  const [hasSubmitted, setHasSubmitted] = useState(false)

  const fetchGame = async () => {
    const res = await api.get('/games')
    const g = res.data.find(x => String(x.id) === String(gameId))
    setGame(g)
    // if logged in, check if user already submitted for this game
    try {
      if (user && user.id) {
        const sres = await api.get(`/submissions/game/${gameId}`)
        const subs = sres.data || []
        const my = subs.find(s => s.userId === user.id)
        if (my) setHasSubmitted(true)
      }
    } catch (e) {
      // ignore
    }
  }

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
      const created = res.data
      showSnackbar('Submitted', 'success')
      // navigate to the game's submissions and select the submission day
      const date = created?.createdAt ? new Date(created.createdAt).toISOString().split('T')[0] : new Date().toISOString().split('T')[0]
      navigate(`/games/${gameId}`, { state: { selectedDate: date } })
    } catch (err) {
      showSnackbar('Failed to submit', 'error')
    }
  }

  // create/revoke preview URL when screenshot changes
  useEffect(() => {
    if (!screenshot) {
      setPreviewUrl(null)
      return
    }
    const url = URL.createObjectURL(screenshot)
    setPreviewUrl(url)
    return () => { URL.revokeObjectURL(url) }
  }, [screenshot])

  // handle paste events (Ctrl+V) to accept images from clipboard
  useEffect(() => {
    const handler = (e) => {
      try {
        const items = e.clipboardData?.items
        if (!items) return
        for (let i = 0; i < items.length; i++) {
          const item = items[i]
          if (item && item.type && item.type.startsWith('image/')) {
            const blob = item.getAsFile ? item.getAsFile() : null
            if (blob) {
              const file = new File([blob], 'clipboard.png', { type: blob.type })
              setScreenshot(file)
              showSnackbar('Image pasted from clipboard', 'success')
              e.preventDefault()
              return
            }
          }
        }
      } catch (err) {
        // ignore
      }
    }
    window.addEventListener('paste', handler)
    return () => window.removeEventListener('paste', handler)
  }, [showSnackbar])   

  if (!game) return <div>Loading...</div>

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
            <input type="file" accept="image/*" onChange={e => setScreenshot(e.target.files?.[0] ?? null)} />
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <div style={{ fontSize: 12, color: '#666' }}>You can paste an image from clipboard (Ctrl+V).</div>
            </div>
            {previewUrl && (
              <div style={{ marginTop: 8 }}>
                <img src={previewUrl} alt="preview" style={{ maxWidth: 320, display: 'block', marginBottom: 6 }} />
                <Button onClick={() => setScreenshot(null)} size="small">Remove</Button>
              </div>
            )}
            <Button type="submit" className="btn">Submit</Button>
          </Stack>
        </form>
      </div>
      
    </div>
  )
}
