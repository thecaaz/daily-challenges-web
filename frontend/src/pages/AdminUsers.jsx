import React, { useEffect, useState } from 'react'
import { Paper, Table, TablePagination, InputAdornment, CircularProgress, TableHead, TableRow, TableCell, TableBody, TextField, Dialog, DialogTitle, DialogContent, DialogActions, Typography, Box } from '@mui/material'
import { Link } from 'react-router-dom'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import useRequireAdmin from '../hooks/useRequireAdmin'
import AdminUserAuditModal from '../components/AdminUserAuditModal'
import useConfirm from '../hooks/useConfirm'
import ConfirmDialog from '../components/ui/ConfirmDialog'
import usePrompt from '../hooks/usePrompt'
import PromptDialog from '../components/ui/PromptDialog'
import formatNumber from '../utils/formatNumber'
import { getApiErrorMessage } from '../utils/apiError'
import useAsyncData from '../hooks/useAsyncData'
import SearchIcon from '@mui/icons-material/Search'

const PAGE_SIZE_OPTIONS = [25, 50, 100]
const PASSWORD_MIN = 8
const PASSWORD_MAX = 128

/** Parse an XP amount field: a non-zero whole number, or null. */
export function parseXpAmount(value) {
  const s = String(value ?? '').trim()
  if (!/^[+-]?\d+$/.test(s)) return null
  const n = Number(s)
  if (!Number.isSafeInteger(n) || n === 0) return null
  return Math.abs(n)
}

export default function AdminUsers() {
  const { user, isAuthorized } = useRequireAdmin()
  const { showSnackbar } = useSnackbar()

  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [deltaById, setDeltaById] = useState({})
  const [auditUserId, setAuditUserId] = useState(null)
  const [auditUsername, setAuditUsername] = useState(null)
  const [auditOpen, setAuditOpen] = useState(false)
  const { confirm, dialogProps } = useConfirm()
  const { prompt, dialogProps: promptDialogProps } = usePrompt()

  const [setPasswordUserId, setSetPasswordUserId] = useState(null)
  const [setPasswordValue, setSetPasswordValue] = useState('')
  const [setPasswordError, setSetPasswordError] = useState('')

  // Debounce the search box; a new search starts at page 1.
  useEffect(() => {
    const term = searchInput.trim()
    if (term === search) return undefined
    const t = setTimeout(() => {
      setSearch(term)
      setPage(1)
    }, 300)
    return () => clearTimeout(t)
  }, [searchInput, search])

  const { data, loading, error, refetch } = useAsyncData(async () => {
    if (!isAuthorized) return null
    const params = { page, pageSize }
    if (search) params.search = search
    const res = await api.get('/admin/users', { params })
    return res.data
  }, [isAuthorized, page, pageSize, search])

  useEffect(() => {
    if (error) showSnackbar('Failed to load users', 'error')
  }, [error, showSnackbar])

  const users = data?.items || []
  // Keep the last known total while a new page loads (data is null then), so
  // the pagination control doesn't jump back to 0 rows.
  const [totalCount, setTotalCount] = useState(0)
  useEffect(() => {
    if (data) setTotalCount(data.totalCount || 0)
  }, [data])

  const adjust = async (id, sign) => {
    const amount = parseXpAmount(deltaById[id])
    if (amount == null) {
      showSnackbar('Enter a whole number of XP (not 0)', 'error')
      return
    }
    const delta = sign * amount
    const reason = await prompt({ title: 'XP Adjustment', message: `${delta >= 0 ? 'Adding' : 'Deducting'} ${formatNumber(amount)} XP`, label: 'Reason (optional)', confirmText: 'Apply' })
    if (reason === null) return
    try {
      await api.post(`/admin/users/${id}/xp`, { delta, reason })
      showSnackbar('XP adjusted', 'success')
      setDeltaById(prev => ({ ...prev, [id]: '' }))
      refetch()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to adjust XP'), 'error')
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
    if (setPasswordValue.length < PASSWORD_MIN || setPasswordValue.length > PASSWORD_MAX) {
      setSetPasswordError(`Password must be ${PASSWORD_MIN}–${PASSWORD_MAX} characters`)
      return
    }
    try {
      await api.post(`/admin/users/${setPasswordUserId}/password`, { newPassword: setPasswordValue })
      showSnackbar('Password updated', 'success')
      closeSetPassword()
    } catch (err) {
      setSetPasswordError(getApiErrorMessage(err, 'Failed to set password'))
    }
  }

  const deleteUser = async (id) => {
    const ok = await confirm({ title: 'Delete user?', message: 'This will permanently delete this user. This cannot be undone.' })
    if (!ok) return
    try {
      await api.delete(`/admin/users/${id}`)
      showSnackbar('User deleted', 'success')
      refetch()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to delete user'), 'error')
    }
  }

  if (!isAuthorized) return null

  return (
    <>
      <Typography variant="h5" sx={{ mb: 2 }}>Admin — Manage Users</Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <TextField
          size="small"
          label="Search username"
          value={searchInput}
          onChange={e => setSearchInput(e.target.value)}
          InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }}
        />
        {loading && <CircularProgress size={20} />}
      </Box>
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
                <TableCell>{u.id ? <Box component={Link} to={`/users/${u.id}`} sx={{ textDecoration: 'none', color: 'inherit' }}>{u.username}</Box> : u.username}</TableCell>
                <TableCell>{formatNumber(u.totalXp)}</TableCell>
                <TableCell>{u.level}</TableCell>
                <TableCell>{formatNumber(u.xpToNextLevel)}</TableCell>
                <TableCell>{u.streak}</TableCell>
                <TableCell style={{ whiteSpace: 'nowrap' }}>
                  <TextField
                    type="number"
                    size="small"
                    value={deltaById[u.id] ?? ''}
                    onChange={e => setDeltaById(prev => ({ ...prev, [u.id]: e.target.value }))}
                    error={(deltaById[u.id] ?? '') !== '' && parseXpAmount(deltaById[u.id]) == null}
                    inputProps={{ step: 1, min: 1, 'aria-label': `XP amount for ${u.username}` }}
                    style={{ width: 100, marginRight: 8 }}
                  />
                  <AppButton variant="contained" disabled={parseXpAmount(deltaById[u.id]) == null} onClick={() => adjust(u.id, 1)}>Add</AppButton>
                  <AppButton variant="outlined" color="error" disabled={parseXpAmount(deltaById[u.id]) == null} onClick={() => adjust(u.id, -1)} style={{ marginLeft: 8 }}>Deduct</AppButton>
                  <AppButton variant="text" onClick={() => { setAuditUserId(u.id); setAuditUsername(u.username); setAuditOpen(true) }} style={{ marginLeft: 8 }}>Audit</AppButton>
                  <AppButton variant="outlined" onClick={() => openSetPassword(u.id)} style={{ marginLeft: 8 }}>Set Password</AppButton>
                  {!u.isAdmin && u.id !== user?.id && (
                    <AppButton variant="outlined" color="error" onClick={() => deleteUser(u.id)} style={{ marginLeft: 8 }}>Delete</AppButton>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {!loading && users.length === 0 && (
              <TableRow>
                <TableCell colSpan={7}>{error ? 'Failed to load users.' : 'No users found.'}</TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={totalCount}
          page={Math.max(0, page - 1)}
          onPageChange={(_, p) => setPage(p + 1)}
          rowsPerPage={pageSize}
          rowsPerPageOptions={PAGE_SIZE_OPTIONS}
          onRowsPerPageChange={e => { setPageSize(Number(e.target.value)); setPage(1) }}
        />
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
