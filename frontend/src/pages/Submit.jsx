import React, { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { TextField, Button, Stack, Typography, Snackbar, Alert } from '@mui/material'
import api from '../api'

export default function Submit() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [username, setUsername] = useState('')
  const [score, setScore] = useState('')
  const [screenshot, setScreenshot] = useState(null)

  useEffect(() => { fetchGame() }, [])

  const fetchGame = async () => {
    const res = await api.get('/games')
    const g = res.data.find(x => String(x.id) === String(gameId))
    setGame(g)
  }

  const submit = async (e) => {
    e.preventDefault()
    const fd = new FormData()
    fd.append('gameId', gameId)
    fd.append('score', score)
    if (username) fd.append('username', username)
    if (screenshot) fd.append('screenshot', screenshot)
    try {
      const res = await api.post('/submissions', fd)
      const created = res.data
      setToast({ open: true, severity: 'success', message: 'Submitted' })
      // navigate to the game's submissions and select the submission day
      const date = created?.createdAt ? new Date(created.createdAt).toISOString().split('T')[0] : new Date().toISOString().split('T')[0]
      navigate(`/games/${gameId}`, { state: { selectedDate: date } })
    } catch (err) {
      setToast({ open: true, severity: 'error', message: 'Failed to submit' })
    }
  }

  const navigate = useNavigate()
  const [toast, setToast] = useState({ open: false, severity: 'success', message: '' })
  const handleClose = () => setToast(t => ({ ...t, open: false }))

  if (!game) return <div>Loading...</div>

  return (
    <div>
      <Typography variant="h5">Submit for {game.name}</Typography>
      <form onSubmit={submit}>
        <Stack spacing={2} maxWidth={480} sx={{ mt: 2 }}>
          <TextField label="Username (optional)" value={username} onChange={e => setUsername(e.target.value)} />
          <TextField label="Score" value={score} onChange={e => setScore(e.target.value)} required />
          <input type="file" accept="image/*" onChange={e => setScreenshot(e.target.files?.[0] ?? null)} />
          <Button type="submit" variant="contained">Submit</Button>
        </Stack>
      </form>
      <Snackbar open={toast.open} autoHideDuration={3000} onClose={handleClose}>
        <Alert onClose={handleClose} severity={toast.severity} sx={{ width: '100%' }}>
          {toast.message}
        </Alert>
      </Snackbar>
    </div>
  )
}
