import React, { useEffect, useState } from 'react'
import { Paper, Table, TableHead, TableRow, TableCell, TableBody, Button, TextField, Dialog, DialogTitle, DialogContent, DialogActions } from '@mui/material'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { useAuth } from '../contexts/AuthContext'
import { useNavigate } from 'react-router-dom'
import AdminUserAuditModal from '../components/AdminUserAuditModal'

export default function AdminUsers() {
  const { user, loading } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const [users, setUsers] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(50)
  const [totalCount, setTotalCount] = useState(0)
  const [deltaById, setDeltaById] = useState({})
  const [auditUserId, setAuditUserId] = useState(null)
  const [auditUsername, setAuditUsername] = useState(null)
  const [auditOpen, setAuditOpen] = useState(false)

  const [setPasswordUserId, setSetPasswordUserId] = useState(null)
  const [setPasswordValue, setSetPasswordValue] = useState('')
  const [setPasswordError, setSetPasswordError] = useState('')

  useEffect(() => { fetchUsers() }, [])

  if (!loading && (!user || !user.isAdmin)) {
    navigate('/')
    return null
  }

  const fetchUsers = async () => {
    try {
      const res = await api.get('/admin/users', { params: { page, pageSize } })
      setUsers(res.data.items || [])
      setTotalCount(res.data.totalCount || 0)
    } catch (err) {
      showSnackbar('Failed to load users', 'error')
    }
  }

  const adjust = async (id, delta) => {
    const reason = window.prompt('Reason for adjustment (optional):') || ''
    try {
      const res = await api.post(`/admin/users/${id}/xp`, { delta, reason })
      showSnackbar('XP adjusted', 'success')
      // Update single row
      setUsers(users.map(u => u.id === res.data.id ? res.data : u))
    } catch (err) {
      showSnackbar('Failed to adjust XP', 'error')
    }
  }

  const openSetPassword = (id) => {
    setSetPasswordUserId(id)
    setSetPasswordValue('')
    setSetPasswordError('')
  }

  const closeSetPassword = () => {
    setSetPasswordUserId(null)
    setSetPasswordValue('')
    setSetPasswordError('')
  }

  const confirmSetPassword = async () => {
    if (!setPasswordValue) {
      setSetPasswordError('Password cannot be empty')
      return
    }
    try {
      await api.post(`/admin/users/${setPasswordUserId}/password`, { newPassword: setPasswordValue })
      showSnackbar('Password updated', 'success')
      closeSetPassword()
    } catch (err) {
      setSetPasswordError(err?.response?.data?.message || 'Failed to set password')
    }
  }

  const deleteUser = async (id) => {
    if (!window.confirm('Delete this user? This cannot be undone.')) return
    try {
      await api.delete(`/admin/users/${id}`)
      showSnackbar('User deleted', 'success')
      setUsers(users.filter(u => u.id !== id))
    } catch (err) {
      showSnackbar(err?.response?.data?.message || 'Failed to delete user', 'error')
    }
  }

  return (
    <>
      <h2>Admin — Manage Users</h2>
      <Paper style={{ padding: 12 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Username</TableCell>
              <TableCell>Total XP</TableCell>
              <TableCell>Level</TableCell>
              <TableCell>XP To Next</TableCell>
              <TableCell>Streak</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map(u => (
              <TableRow key={u.id}>
                <TableCell>{u.id}</TableCell>
                <TableCell>{u.username}</TableCell>
                <TableCell>{u.totalXp}</TableCell>
                <TableCell>{u.level}</TableCell>
                <TableCell>{u.xpToNextLevel}</TableCell>
                <TableCell>{u.streak}</TableCell>
                <TableCell style={{ whiteSpace: 'nowrap' }}>
                  <TextField
                    type="number"
                    size="small"
                    value={deltaById[u.id] ?? ''}
                    onChange={e => setDeltaById({ ...deltaById, [u.id]: e.target.value })}
                    style={{ width: 100, marginRight: 8 }}
                  />
                  <Button variant="contained" onClick={() => adjust(u.id, Math.abs(Number(deltaById[u.id] || 0)))}>Add</Button>
                  <Button variant="outlined" color="error" onClick={() => adjust(u.id, -Math.abs(Number(deltaById[u.id] || 0)))} style={{ marginLeft: 8 }}>Deduct</Button>
                  <Button variant="text" onClick={() => { setAuditUserId(u.id); setAuditUsername(u.username); setAuditOpen(true) }} style={{ marginLeft: 8 }}>Audit</Button>
                  <Button variant="outlined" onClick={() => openSetPassword(u.id)} style={{ marginLeft: 8 }}>Set Password</Button>
                  {!u.isAdmin && u.id !== user?.id && (
                    <Button variant="outlined" color="error" onClick={() => deleteUser(u.id)} style={{ marginLeft: 8 }}>Delete</Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <Dialog open={setPasswordUserId !== null} onClose={closeSetPassword}>
        <DialogTitle>Set Password</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="New Password"
            type="password"
            fullWidth
            value={setPasswordValue}
            onChange={e => { setSetPasswordValue(e.target.value); setSetPasswordError('') }}
            error={!!setPasswordError}
            helperText={setPasswordError}
            style={{ marginTop: 8 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={closeSetPassword}>Cancel</Button>
          <Button variant="contained" onClick={confirmSetPassword}>Confirm</Button>
        </DialogActions>
      </Dialog>

      <AdminUserAuditModal open={auditOpen} onClose={() => setAuditOpen(false)} userId={auditUserId} username={auditUsername} />
    </>
  )
}
