import React, { useState, useEffect } from 'react'
import { TextField, Stack, Paper, Table, TableHead, TableRow, TableCell, TableBody, IconButton, Typography, Box } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import AppButton from '../components/ui/AppButton'
import useImageUpload from '../hooks/useImageUpload'
import ImageUpload from '../components/ui/ImageUpload/ImageUpload'
import api from '../api'
import { formatDateTime } from '../utils/dateFormat'
import parseScore from '../utils/parseScore'
import { useSnackbar } from '../contexts/SnackbarContext'
import useRequireAdmin from '../hooks/useRequireAdmin'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import useConfirm from '../hooks/useConfirm'
import ConfirmDialog from '../components/ui/ConfirmDialog'

export default function Admin() {
  const [name, setName] = useState('')
  const { showSnackbar } = useSnackbar()
  const { confirm, dialogProps } = useConfirm()
  const {
    screenshot: createImageFile,
    previewUrl: createPreviewUrl,
    onFileChange: onCreateFileChange,
    clear: clearCreateImage
  } = useImageUpload(showSnackbar)

  const {
    screenshot: editImageFile,
    previewUrl: editPreviewUrl,
    onFileChange: onEditFileChange,
    clear: clearEditImage
  } = useImageUpload(showSnackbar)
  const [resetTime, setResetTime] = useState('00:00')
  const [url, setUrl] = useState('')
  const [description, setDescription] = useState('')
  const [rankingMode, setRankingMode] = useState('highest')
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
    if (description) fd.append('description', description)
    fd.append('resetTime', resetTime)
    fd.append('resetTimezoneId', detectedTimezone)
    fd.append('rankingMode', rankingMode)
    if (createImageFile) fd.append('image', createImageFile)
    try {
      await api.post('/games', fd)
      setName('')
      setDescription('')
      setRankingMode('highest')
      clearCreateImage()
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
  const { isAuthorized } = useRequireAdmin()

  const goUsers = () => navigate('/admin/users')

  if (!isAuthorized) return null

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
      if (editImageFile) fd.append('image', editImageFile)
      else if (editingGame.imageFile) fd.append('image', editingGame.imageFile)
      if (editingGame.description !== undefined) fd.append('description', editingGame.description ?? '')
      fd.append('rankingMode', editingGame.rankingMode ?? 'highest')
      await api.put(`/games/${editingGame.id}`, fd)
      showSnackbar('Game updated', 'success')
      setEditingGame(null)
      clearEditImage()
      fetchGames()
    } catch (err) {
      showSnackbar('Failed to update game', 'error')
    }
  }

  const removeGame = async (id) => {
    const ok = await confirm({ title: 'Delete game?', message: 'This will permanently remove the game and all its submissions.' })
    if (!ok) return
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
      const parsed = parseScore(s.score)
      await api.put(`/submissions/${s.id}`, { score: isNaN(parsed) ? s.score : String(parsed) })
      showSnackbar('Submission updated', 'success')
      manageSubs(selectedGameId)
    } catch (err) {
      showSnackbar('Failed to update submission', 'error')
    }
  }

  const deleteSubmission = async (id) => {
    const ok = await confirm({ title: 'Delete submission?', message: 'This submission will be permanently removed.' })
    if (!ok) return
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
    <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1.5 }}>
      <AppButton onClick={goUsers} sx={{ mb: 2 }}>Manage Users</AppButton>
    </Box>
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={name} onChange={e => setName(e.target.value)} required />
        <TextField label="Game URL (optional)" value={url} onChange={e => setUrl(e.target.value)} />
          <ImageUpload onFileChange={onCreateFileChange} previewUrl={createPreviewUrl} onRemove={clearCreateImage} />
          <div>
            <label>Reset time (day boundary): </label>
            <input type="time" value={resetTime} onChange={e => setResetTime(e.target.value)} />
          </div>
          <div>
            <label>Reset timezone (detected): </label>
            <input type="text" value={detectedTimezone} readOnly />
          </div>
        <div>
            <label>Ranking mode: </label>
            <select value={rankingMode} onChange={e => setRankingMode(e.target.value)}>
              <option value="highest">Highest (higher score wins)</option>
              <option value="lowest">Lowest (lower score wins)</option>
            </select>
          </div>
        <AppButton type="submit">Create Game</AppButton>
      </Stack>
      
    </form>

    <Typography variant="h5" sx={{ mt: 3, mb: 1 }}>Manage Games</Typography>
    <div>
      {games.map(g => (
        <Paper key={g.id} sx={{ p: 1.5, mb: 1.5 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <strong>{g.name}</strong> {g.url ? <a href={g.url} target="_blank" rel="noreferrer">(link)</a> : null}
            </div>
            <div>
              <IconButton onClick={() => startEdit(g)} title="Edit"><EditIcon /></IconButton>
              <IconButton onClick={() => removeGame(g.id)} title="Delete"><DeleteIcon /></IconButton>
              <AppButton onClick={() => manageSubs(g.id)} sx={{ ml: 1 }}>Manage Submissions</AppButton>
            </div>
          </Box>
        </Paper>
      ))}
    </div>

    {editingGame && (
      <Paper sx={{ p: 1.5, mt: 1.5 }}>
        <Typography variant="h6" sx={{ mb: 1 }}>Edit Game</Typography>
        <Stack spacing={2} maxWidth={600}>
          <TextField label="Name" value={editingGame.name} onChange={e => setEditingGame({ ...editingGame, name: e.target.value })} />
          <TextField label="URL" value={editingGame.url ?? ''} onChange={e => setEditingGame({ ...editingGame, url: e.target.value })} />
          <TextField label="Description" value={editingGame.description ?? ''} onChange={e => setEditingGame({ ...editingGame, description: e.target.value })} multiline rows={3} />
          <div>
            <label>Reset time: </label>
            <input type="time" value={editingGame.resetTime ?? '00:00'} onChange={e => setEditingGame({ ...editingGame, resetTime: e.target.value })} />
          </div>
          <div>
            <label>Ranking mode: </label>
            <select value={editingGame.rankingMode ?? 'highest'} onChange={e => setEditingGame({ ...editingGame, rankingMode: e.target.value })}>
              <option value="highest">Highest (higher score wins)</option>
              <option value="lowest">Lowest (lower score wins)</option>
            </select>
          </div>
          <div>
            <ImageUpload onFileChange={onEditFileChange} previewUrl={editPreviewUrl} onRemove={clearEditImage} />
          </div>
          <div>
            <AppButton onClick={saveEdit}>Save</AppButton>
            <AppButton onClick={() => setEditingGame(null)}>Cancel</AppButton>
          </div>
        </Stack>
      </Paper>
    )}

    {selectedGameId && (
      <Paper sx={{ p: 1.5, mt: 1.5 }}>
        <Typography variant="h6" sx={{ mb: 1 }}>Submissions</Typography>
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
                  {s.score !== '' && s.score != null && isNaN(parseScore(s.score)) && (
                    <div style={{ color: '#ed6c02', fontSize: '0.75rem', marginTop: 2 }}>Not a number</div>
                  )}
                </TableCell>
                <TableCell>{formatDateTime(s.createdAt)}</TableCell>
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
    <ConfirmDialog {...dialogProps} />
    </>
  )
}
