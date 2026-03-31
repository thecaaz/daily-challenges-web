import React, { useState, useEffect } from 'react'
import { TextField, Stack, Paper, Table, TableHead, TableRow, TableCell, TableBody, IconButton } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import parseUtcDate from '../utils/parseUtcDate'
import { useSnackbar } from '../contexts/SnackbarContext'
import { useAuth } from '../contexts/AuthContext'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'

export default function Admin() {
  const [name, setName] = useState('')
  const [image, setImage] = useState(null)
  const { showSnackbar } = useSnackbar()
  const [resetTime, setResetTime] = useState('00:00')
  const [url, setUrl] = useState('')
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
    if (url) fd.append('url', url)
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
      fetchGames()
    } catch (err) {
      showSnackbar('Failed to create game', 'error')
    }
  }

  const navigate = useNavigate()
  const { user, loading } = useAuth()

  const goUsers = () => navigate('/admin/users')

  if (!loading && (!user || !user.isAdmin)) {
    navigate('/')
    return null
  }

  const [games, setGames] = useState([])
  const [editingGame, setEditingGame] = useState(null)
  const [submissions, setSubmissions] = useState([])
  const [selectedGameId, setSelectedGameId] = useState(null)

  useEffect(() => { fetchGames() }, [])

  const fetchGames = async () => {
    try {
      const res = await api.get('/games')
      setGames(res.data)
    } catch (err) {
      showSnackbar('Failed to load games', 'error')
    }
  }

  const startEdit = (g) => {
    setEditingGame({ ...g })
  }

  const saveEdit = async () => {
    try {
      const fd = new FormData()
      if (editingGame.name) fd.append('name', editingGame.name)
      if (editingGame.url) fd.append('url', editingGame.url)
      if (editingGame.resetTime) fd.append('resetTime', editingGame.resetTime)
      if (editingGame.resetTimezoneId) fd.append('resetTimezoneId', editingGame.resetTimezoneId)
      if (editingGame.imageFile) fd.append('image', editingGame.imageFile)
      await api.put(`/games/${editingGame.id}`, fd)
      showSnackbar('Game updated', 'success')
      setEditingGame(null)
      fetchGames()
    } catch (err) {
      showSnackbar('Failed to update game', 'error')
    }
  }

  const removeGame = async (id) => {
    if (!confirm('Delete game? This will remove submissions.')) return
    try {
      await api.delete(`/games/${id}`)
      showSnackbar('Game deleted', 'success')
      fetchGames()
    } catch (err) {
      showSnackbar('Failed to delete game', 'error')
    }
  }

  const manageSubs = async (gameId) => {
    setSelectedGameId(gameId)
    try {
      const res = await api.get(`/submissions/game/${gameId}/unfiltered`)
      setSubmissions(res.data || [])
    } catch (err) {
      showSnackbar('Failed to load submissions', 'error')
    }
  }

  const updateSubmission = async (s) => {
    try {
      await api.put(`/submissions/${s.id}`, { score: s.score })
      showSnackbar('Submission updated', 'success')
      manageSubs(selectedGameId)
    } catch (err) {
      showSnackbar('Failed to update submission', 'error')
    }
  }

  const deleteSubmission = async (id) => {
    if (!confirm('Delete submission?')) return
    try {
      await api.delete(`/submissions/${id}`)
      showSnackbar('Submission deleted', 'success')
      manageSubs(selectedGameId)
    } catch (err) {
      showSnackbar('Failed to delete submission', 'error')
    }
  }

  return (
    <>
    <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
      <AppButton onClick={goUsers} sx={{ mb: 2 }}>Manage Users</AppButton>
    </div>
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={name} onChange={e => setName(e.target.value)} required />
        <TextField label="Game URL (optional)" value={url} onChange={e => setUrl(e.target.value)} />
          <input type="file" accept="image/*" onChange={e => setImage(e.target.files?.[0] ?? null)} />
          <div>
            <label>Reset time (day boundary): </label>
            <input type="time" value={resetTime} onChange={e => setResetTime(e.target.value)} />
          </div>
          <div>
            <label>Reset timezone (detected): </label>
            <input type="text" value={detectedTimezone} readOnly />
          </div>
        <AppButton type="submit">Create Game</AppButton>
      </Stack>
      
    </form>

    <h2 style={{ marginTop: 24 }}>Manage Games</h2>
    <div>
      {games.map(g => (
        <Paper key={g.id} style={{ padding: 12, marginBottom: 12 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <strong>{g.name}</strong> {g.url ? <a href={g.url} target="_blank" rel="noreferrer">(link)</a> : null}
            </div>
            <div>
              <IconButton onClick={() => startEdit(g)} title="Edit"><EditIcon /></IconButton>
              <IconButton onClick={() => removeGame(g.id)} title="Delete"><DeleteIcon /></IconButton>
              <AppButton onClick={() => manageSubs(g.id)} sx={{ ml: 1 }}>Manage Submissions</AppButton>
            </div>
          </div>
        </Paper>
      ))}
    </div>

    {editingGame && (
      <Paper style={{ padding: 12, marginTop: 12 }}>
        <h3>Edit Game</h3>
        <Stack spacing={2} maxWidth={600}>
          <TextField label="Name" value={editingGame.name} onChange={e => setEditingGame({ ...editingGame, name: e.target.value })} />
          <TextField label="URL" value={editingGame.url ?? ''} onChange={e => setEditingGame({ ...editingGame, url: e.target.value })} />
          <div>
            <label>Reset time: </label>
            <input type="time" value={editingGame.resetTime ?? '00:00'} onChange={e => setEditingGame({ ...editingGame, resetTime: e.target.value })} />
          </div>
          <div>
            <input type="file" accept="image/*" onChange={e => setEditingGame({ ...editingGame, imageFile: e.target.files?.[0] ?? null })} />
          </div>
          <div>
            <AppButton onClick={saveEdit}>Save</AppButton>
            <AppButton onClick={() => setEditingGame(null)}>Cancel</AppButton>
          </div>
        </Stack>
      </Paper>
    )}

    {selectedGameId && (
      <Paper style={{ padding: 12, marginTop: 12 }}>
        <h3>Submissions</h3>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Username</TableCell>
              <TableCell>Score</TableCell>
              <TableCell>Created</TableCell>
              <TableCell></TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {submissions.map(s => (
              <TableRow key={s.id}>
                <TableCell>{s.id}</TableCell>
                <TableCell>{s.username ?? ''}</TableCell>
                <TableCell>
                  <input value={s.score ?? ''} onChange={e => setSubmissions(submissions.map(x => x.id === s.id ? { ...x, score: e.target.value } : x))} />
                </TableCell>
                <TableCell>{parseUtcDate(s.createdAt).toLocaleString()}</TableCell>
                <TableCell>
                  <AppButton onClick={() => updateSubmission(s)}>Save</AppButton>
                  <AppButton color="error" onClick={() => deleteSubmission(s.id)}>Delete</AppButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    )}
    </>
  )
}
