import React, { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import { TextField, Button, Stack, Typography } from '@mui/material'
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
    await api.post('/submissions', fd)
    alert('Submitted')
    setScore('')
    setScreenshot(null)
  }

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
    </div>
  )
}
