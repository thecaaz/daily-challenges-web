import React, { useState, useEffect, useCallback, useRef } from 'react'
import { TextField, Stack, Paper, Table, TableHead, TableRow, TableCell, TableBody, IconButton, Typography, Box } from '@mui/material'
import { useNavigate, Link } from 'react-router-dom'
import PlayButton from '../components/ui/PlayButton'
import AppButton from '../components/ui/AppButton'
import useImageUpload from '../hooks/useImageUpload'
import ImageUpload from '../components/ui/ImageUpload/ImageUpload'
import api from '../api'
import { formatDateTime } from '../utils/dateFormat'
import parseScore from '../utils/parseScore'
import { utcToLocalHHmm, localToUtcHHmm } from '../utils/resetTime'
import { getApiErrorMessage } from '../utils/apiError'
import { useSnackbar } from '../contexts/SnackbarContext'
import useRequireAdmin from '../hooks/useRequireAdmin'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import useConfirm from '../hooks/useConfirm'
import ConfirmDialog from '../components/ui/ConfirmDialog'

// The API stores and returns the reset time as UTC "HH:mm". The forms show and
// accept the admin's local time and convert on the way in and out.
function toLocal(utcHHmm) {
  return utcToLocalHHmm(utcHHmm) ?? '00:00'
}

export default function Admin() {
  const navigate = useNavigate()
  const { isAuthorized } = useRequireAdmin()
  const { showSnackbar } = useSnackbar()
  const { confirm, dialogProps } = useConfirm()

  const [name, setName] = useState('')
  const [localResetTime, setLocalResetTime] = useState('00:00')
  const [url, setUrl] = useState('')
  const [description, setDescription] = useState('')
  const [rankingMode, setRankingMode] = useState('highest')

  const [games, setGames] = useState([])
  const [editingGame, setEditingGame] = useState(null)
  const [submissions, setSubmissions] = useState([])
  const [selectedGameId, setSelectedGameId] = useState(null)
  const subsRequestRef = useRef(0)

  // Only the active form listens for clipboard pastes: the edit form while a
  // game is being edited, the create form otherwise.
  const {
    screenshot: createImageFile,
    previewUrl: createPreviewUrl,
    onFileChange: onCreateFileChange,
    clear: clearCreateImage
  } = useImageUpload(showSnackbar, { enabled: !editingGame })

  const {
    screenshot: editImageFile,
    previewUrl: editPreviewUrl,
    onFileChange: onEditFileChange,
    clear: clearEditImage
  } = useImageUpload(showSnackbar, { enabled: !!editingGame })

  const fetchGames = useCallback(async () => {
    try {
      const res = await api.get('/games')
      setGames(res.data || [])
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to load games'), 'error')
    }
  }, [showSnackbar])

  // Only load once the admin check has passed.
  useEffect(() => { if (isAuthorized) fetchGames() }, [isAuthorized, fetchGames])

  const submit = async (e) => {
    e.preventDefault()
    const utcResetTime = localToUtcHHmm(localResetTime)
    if (!utcResetTime) {
      showSnackbar('Enter a valid reset time', 'error')
      return
    }
    const fd = new FormData()
    fd.append('name', name.trim())
    fd.append('url', url.trim())
    fd.append('description', description)
    fd.append('resetTime', utcResetTime)
    fd.append('rankingMode', rankingMode)
    if (createImageFile) fd.append('image', createImageFile)
    try {
      await api.post('/games', fd)
      setName('')
      setUrl('')
      setDescription('')
      setRankingMode('highest')
      clearCreateImage()
      setLocalResetTime('00:00')
      showSnackbar('Game created', 'success')
      // navigate back to games overview
      navigate('/')
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to create game'), 'error')
    }
  }

  const startEdit = (g) => {
    clearEditImage()
    setEditingGame({ ...g, localResetTime: toLocal(g.resetTime) })
  }

  const cancelEdit = () => {
    clearEditImage()
    setEditingGame(null)
  }

  const saveEdit = async () => {
    const utcResetTime = localToUtcHHmm(editingGame.localResetTime)
    if (!utcResetTime) {
      showSnackbar('Enter a valid reset time', 'error')
      return
    }
    try {
      const fd = new FormData()
      if (editingGame.name) fd.append('name', editingGame.name.trim())
      // Always sent: an empty string clears the field.
      fd.append('url', (editingGame.url ?? '').trim())
      fd.append('description', editingGame.description ?? '')
      fd.append('resetTime', utcResetTime)
      fd.append('rankingMode', editingGame.rankingMode ?? 'highest')
      if (editImageFile) fd.append('image', editImageFile)
      await api.put(`/games/${editingGame.id}`, fd)
      showSnackbar('Game updated', 'success')
      cancelEdit()
      fetchGames()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to update game'), 'error')
    }
  }

  const removeGame = async (id) => {
    const ok = await confirm({ title: 'Delete game?', message: 'This will permanently remove the game and all its submissions.' })
    if (!ok) return
    try {
      await api.delete(`/games/${id}`)
      showSnackbar('Game deleted', 'success')
      if (editingGame?.id === id) cancelEdit()
      if (selectedGameId === id) { setSelectedGameId(null); setSubmissions([]) }
      fetchGames()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to delete game'), 'error')
    }
  }

  const manageSubs = async (gameId) => {
    // Ignore responses that arrive after a newer request (e.g. the admin
    // switched games quickly).
    const requestId = ++subsRequestRef.current
    if (gameId !== selectedGameId) setSubmissions([])
    setSelectedGameId(gameId)
    try {
      const res = await api.get(`/submissions/game/${gameId}/unfiltered`)
      if (requestId !== subsRequestRef.current) return
      setSubmissions(res.data || [])
    } catch (err) {
      if (requestId !== subsRequestRef.current) return
      showSnackbar(getApiErrorMessage(err, 'Failed to load submissions'), 'error')
    }
  }

  const updateSubmission = async (s) => {
    try {
      const trimmed = String(s.score ?? '').trim()
      await api.put(`/submissions/${s.id}`, { score: trimmed })
      showSnackbar('Submission updated', 'success')
      manageSubs(selectedGameId)
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to update submission'), 'error')
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
      showSnackbar(getApiErrorMessage(err, 'Failed to delete submission'), 'error')
    }
  }

  if (!isAuthorized) return null

  return (
    <>
    <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1.5 }}>
      <AppButton onClick={() => navigate('/admin/users')} sx={{ mb: 2, mr: 1 }}>Manage Users</AppButton>
      <AppButton onClick={() => navigate('/debug/timeguessr')} sx={{ mb: 2 }}>Debug Timeguessr Test</AppButton>
    </Box>
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={name} onChange={e => setName(e.target.value)} required />
        <TextField label="Game URL (optional)" value={url} onChange={e => setUrl(e.target.value)} type="url" helperText="Absolute http(s) URL" />
        <TextField label="Description" value={description} onChange={e => setDescription(e.target.value)} multiline rows={3} />
        <ImageUpload onFileChange={onCreateFileChange} previewUrl={createPreviewUrl} onRemove={clearCreateImage} />
        <div>
          <label>Reset time (day boundary, your local time): </label>
          <input type="time" value={localResetTime} onChange={e => setLocalResetTime(e.target.value)} required />
          {localToUtcHHmm(localResetTime) && (
            <Typography variant="caption" color="text.secondary" sx={{ ml: 1 }}>
              = {localToUtcHHmm(localResetTime)} UTC
            </Typography>
          )}
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
              <strong>{g.name}</strong> {g.url ? <PlayButton game={g}><span style={{ textDecoration: 'none' }}>(link)</span></PlayButton> : null}
              {g.resetTime && (
                <Typography variant="caption" color="text.secondary" component="div">
                  Resets at {toLocal(g.resetTime)} local ({g.resetTime} UTC)
                </Typography>
              )}
            </div>
            <div>
              <IconButton onClick={() => startEdit(g)} title="Edit" aria-label={`Edit ${g.name}`}><EditIcon /></IconButton>
              <IconButton onClick={() => removeGame(g.id)} title="Delete" aria-label={`Delete ${g.name}`}><DeleteIcon /></IconButton>
              <AppButton onClick={() => manageSubs(g.id)} sx={{ ml: 1 }}>Manage Submissions</AppButton>
            </div>
          </Box>
        </Paper>
      ))}
    </div>

    {editingGame && (
      <Paper key={editingGame.id} sx={{ p: 1.5, mt: 1.5 }}>
        <Typography variant="h6" sx={{ mb: 1 }}>Edit Game</Typography>
        <Stack spacing={2} maxWidth={600}>
          <TextField label="Name" value={editingGame.name ?? ''} onChange={e => setEditingGame({ ...editingGame, name: e.target.value })} />
          <TextField label="URL" value={editingGame.url ?? ''} onChange={e => setEditingGame({ ...editingGame, url: e.target.value })} type="url" helperText="Leave empty to remove the URL" />
          <TextField label="Description" value={editingGame.description ?? ''} onChange={e => setEditingGame({ ...editingGame, description: e.target.value })} multiline rows={3} />
          <div>
            <label>Reset time (your local time): </label>
            <input type="time" value={editingGame.localResetTime} onChange={e => setEditingGame({ ...editingGame, localResetTime: e.target.value })} required />
            {localToUtcHHmm(editingGame.localResetTime) && (
              <Typography variant="caption" color="text.secondary" sx={{ ml: 1 }}>
                = {localToUtcHHmm(editingGame.localResetTime)} UTC
              </Typography>
            )}
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
            <AppButton onClick={cancelEdit}>Cancel</AppButton>
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
                <TableCell>{s.userId ? <Box component={Link} to={`/users/${s.userId}`} sx={{ textDecoration: 'none', color: 'inherit' }}>{s.username ?? ''}</Box> : (s.username ?? '')}</TableCell>
                <TableCell>
                  <input value={s.score ?? ''} onChange={e => setSubmissions(prev => prev.map(x => x.id === s.id ? { ...x, score: e.target.value } : x))} />
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
