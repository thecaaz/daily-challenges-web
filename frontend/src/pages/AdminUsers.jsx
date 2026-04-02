import React, { useEffect, useState } from 'react'
import { Paper, Table, TableHead, TableRow, TableCell, TableBody, TextField, Dialog, DialogTitle, DialogContent, DialogActions, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import useRequireAdmin from '../hooks/useRequireAdmin'
import AdminUserAuditModal from '../components/AdminUserAuditModal'
import useConfirm from '../hooks/useConfirm'
import ConfirmDialog from '../components/ui/ConfirmDialog'
import usePrompt from '../hooks/usePrompt'
import PromptDialog from '../components/ui/PromptDialog'

export default function AdminUsers() {
  const { user, isAuthorized } = useRequireAdmin()
  const { showSnackbar } = useSnackbar()

  const [users, setUsers] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(50)
  const [totalCount, setTotalCount] = useState(0)
  const [deltaById, setDeltaById] = useState({})
  const [auditUserId, setAuditUserId] = useState(null)
  const [auditUsername, setAuditUsername] = useState(null)
  const [auditOpen, setAuditOpen] = useState(false)
  const { confirm, dialogProps } = useConfirm()
  const { prompt, dialogProps: promptDialogProps } = usePrompt()

  const [setPasswordUserId, setSetPasswordUserId] = useState(null)
  const [setPasswordValue, setSetPasswordValue] = useState('')
  const [setPasswordError, setSetPasswordError] = useState('')

  useEffect(() => { fetchUsers() }, [])

  if (!isAuthorized) return null

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
    const reason = await prompt({ title: 'XP Adjustment', message: `${delta >= 0 ? 'Adding' : 'Deducting'} ${Math.abs(delta)} XP`, label: 'Reason (optional)', confirmText: 'Apply' })
    if (reason === null) return
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
    const ok = await confirm({ title: 'Delete user?', message: 'This will permanently delete this user. This cannot be undone.' })
    if (!ok) return
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
      <Typography variant="h5" sx={{ mb: 2 }}>Admin — Manage Users</Typography>
      <Paper sx={{ p: 1.5 }}>
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
                  <AppButton variant="contained" onClick={() => adjust(u.id, Math.abs(Number(deltaById[u.id] || 0)))}>Add</AppButton>
                  <AppButton variant="outlined" color="error" onClick={() => adjust(u.id, -Math.abs(Number(deltaById[u.id] || 0)))} style={{ marginLeft: 8 }}>Deduct</AppButton>
                  <AppButton variant="text" onClick={() => { setAuditUserId(u.id); setAuditUsername(u.username); setAuditOpen(true) }} style={{ marginLeft: 8 }}>Audit</AppButton>
                  <AppButton variant="outlined" onClick={() => openSetPassword(u.id)} style={{ marginLeft: 8 }}>Set Password</AppButton>
                  {!u.isAdmin && u.id !== user?.id && (
                    <AppButton variant="outlined" color="error" onClick={() => deleteUser(u.id)} style={{ marginLeft: 8 }}>Delete</AppButton>
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
          <AppButton variant="text" onClick={closeSetPassword}>Cancel</AppButton>
          <AppButton variant="contained" onClick={confirmSetPassword}>Confirm</AppButton>
        </DialogActions>
      </Dialog>

      <AdminUserAuditModal open={auditOpen} onClose={() => setAuditOpen(false)} userId={auditUserId} username={auditUsername} />
      <ConfirmDialog {...dialogProps} />
      <PromptDialog {...promptDialogProps} />
    </>
  )
}
