import React, { useState } from 'react'
import { TextField, Button, Stack, Snackbar, Alert } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import api from '../api'

export default function Admin() {
  const [name, setName] = useState('')
  const [image, setImage] = useState(null)

  const submit = async (e) => {
    e.preventDefault()
    const fd = new FormData()
    fd.append('name', name)
    if (image) fd.append('image', image)
    try {
      await api.post('/games', fd)
      setName('')
      setImage(null)
      setToast({ open: true, severity: 'success', message: 'Game created' })
      // navigate back to games overview
      navigate('/')
    } catch (err) {
      setToast({ open: true, severity: 'error', message: 'Failed to create game' })
    }
  }

  const navigate = useNavigate()
  const [toast, setToast] = useState({ open: false, severity: 'success', message: '' })

  const handleClose = () => setToast(t => ({ ...t, open: false }))

  return (
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={name} onChange={e => setName(e.target.value)} required />
        <input type="file" accept="image/*" onChange={e => setImage(e.target.files?.[0] ?? null)} />
        <Button variant="contained" type="submit">Create Game</Button>
      </Stack>
      <Snackbar open={toast.open} autoHideDuration={3000} onClose={handleClose}>
        <Alert onClose={handleClose} severity={toast.severity} sx={{ width: '100%' }}>
          {toast.message}
        </Alert>
      </Snackbar>
    </form>
  )
}
