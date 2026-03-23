import React, { useState } from 'react'
import { TextField, Button, Stack } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'

export default function Admin() {
  const [name, setName] = useState('')
  const [image, setImage] = useState(null)
  const { showSnackbar } = useSnackbar()
  const [resetTime, setResetTime] = useState('00:00')
  const [detectedTimezone, setDetectedTimezone] = useState(() => {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
    } catch (e) {
      return 'UTC'
    }
  })

  const submit = async (e) => {
    e.preventDefault()
    const fd = new FormData()
    fd.append('name', name)
    fd.append('resetTime', resetTime)
    fd.append('resetTimezoneId', detectedTimezone)
    if (image) fd.append('image', image)
    try {
      await api.post('/games', fd)
      setName('')
      setImage(null)
      setResetTime('00:00')
      showSnackbar('Game created', 'success')
      // navigate back to games overview
      navigate('/')
    } catch (err) {
      showSnackbar('Failed to create game', 'error')
    }
  }

  const navigate = useNavigate()
  

  return (
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={name} onChange={e => setName(e.target.value)} required />
          <input type="file" accept="image/*" onChange={e => setImage(e.target.files?.[0] ?? null)} />
          <div>
            <label>Reset time (day boundary): </label>
            <input type="time" value={resetTime} onChange={e => setResetTime(e.target.value)} />
          </div>
          <div>
            <label>Reset timezone (detected): </label>
            <input type="text" value={detectedTimezone} readOnly />
          </div>
        <Button variant="contained" type="submit">Create Game</Button>
      </Stack>
      
    </form>
  )
}
