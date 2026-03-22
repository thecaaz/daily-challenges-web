import React, { useState } from 'react'
import { TextField, Button, Stack } from '@mui/material'
import api from '../api'

export default function Admin() {
  const [name, setName] = useState('')
  const [image, setImage] = useState(null)

  const submit = async (e) => {
    e.preventDefault()
    const fd = new FormData()
    fd.append('name', name)
    if (image) fd.append('image', image)
    await api.post('/games', fd)
    setName('')
    setImage(null)
    alert('Game created')
  }

  return (
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={name} onChange={e => setName(e.target.value)} required />
        <input type="file" accept="image/*" onChange={e => setImage(e.target.files?.[0] ?? null)} />
        <Button variant="contained" type="submit">Create Game</Button>
      </Stack>
    </form>
  )
}
