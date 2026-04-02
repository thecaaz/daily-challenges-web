import React, { useEffect, useState } from 'react'
import { Dialog, DialogTitle, DialogContent, DialogActions, Table, TableHead, TableRow, TableCell, TableBody, TextField, MenuItem, IconButton } from '@mui/material'
import AppButton from './ui/AppButton'
import CloseIcon from '@mui/icons-material/Close'
import api from '../api'
import { formatDateTimeUtc } from '../utils/dateFormat'
import { useSnackbar } from '../contexts/SnackbarContext'
import downloadBlob from '../utils/downloadBlob'

export default function AdminUserAuditModal({ open, onClose, userId, username }) {
  const { showSnackbar } = useSnackbar()

  const [items, setItems] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [eventType, setEventType] = useState('')

  useEffect(() => {
    if (open) fetchPage(1)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const fetchPage = async (p = page) => {
    if (!userId) return
    try {
      const params = { page: p, pageSize }
      if (from) params.from = from
      if (to) params.to = to
      if (eventType) params.eventType = eventType
      const res = await api.get(`/admin/users/${userId}/xp-events`, { params })
      setItems(res.data.items || [])
      setTotalCount(res.data.totalCount || 0)
      setPage(p)
    } catch (err) {
      showSnackbar('Failed to load XP events', 'error')
    }
  }

  const handleExport = async () => {
    try {
      const params = {}
      if (from) params.from = from
      if (to) params.to = to
      if (eventType) params.eventType = eventType
      const res = await api.get(`/admin/users/${userId}/xp-events/export`, { params, responseType: 'blob' })
      downloadBlob(`xp-events-user-${userId}.csv`, new Blob([res.data]))
    } catch (err) {
      showSnackbar('Failed to export CSV', 'error')
    }
  }

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="lg">
      <DialogTitle>
        XP Audit — {username ?? `User ${userId}`}
        <IconButton aria-label="close" onClick={onClose} sx={{ position: 'absolute', right: 8, top: 8 }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
          <TextField label="From" type="date" size="small" value={from} onChange={e => setFrom(e.target.value)} InputLabelProps={{ shrink: true }} />
          <TextField label="To" type="date" size="small" value={to} onChange={e => setTo(e.target.value)} InputLabelProps={{ shrink: true }} />
          <TextField select label="Event Type" size="small" value={eventType} onChange={e => setEventType(e.target.value)} style={{ minWidth: 160 }}>
            <MenuItem value="">All</MenuItem>
            <MenuItem value="submission">Submission</MenuItem>
            <MenuItem value="streak_bonus">Streak Bonus</MenuItem>
            <MenuItem value="admin_adjustment">Admin Adjustment</MenuItem>
          </TextField>
          <AppButton variant="outlined" onClick={() => fetchPage(1)}>Apply</AppButton>
          <AppButton variant="text" onClick={() => { setFrom(''); setTo(''); setEventType(''); fetchPage(1) }}>Reset</AppButton>
        </div>

        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>When (UTC)</TableCell>
              <TableCell>Amount</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Details</TableCell>
              <TableCell>Submission</TableCell>
              <TableCell>Game</TableCell>
              <TableCell>Scoring Day</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map(it => (
              <TableRow key={it.id}>
                <TableCell>{formatDateTimeUtc(it.createdAt)}</TableCell>
                <TableCell>{it.amount}</TableCell>
                <TableCell>{it.eventType}</TableCell>
                <TableCell style={{ maxWidth: 300, whiteSpace: 'pre-wrap' }}>{it.details}</TableCell>
                <TableCell>{it.submissionId ?? ''}</TableCell>
                <TableCell>{it.gameId ?? ''}</TableCell>
                <TableCell>{it.scoringDay ?? ''}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12 }}>
          <div>Showing {items.length} of {totalCount}</div>
          <div>
            <AppButton variant="text" disabled={page <= 1} onClick={() => fetchPage(page - 1)}>Prev</AppButton>
            <AppButton variant="text" disabled={page * pageSize >= totalCount} onClick={() => fetchPage(page + 1)}>Next</AppButton>
          </div>
        </div>
      </DialogContent>
      <DialogActions>
        <AppButton variant="text" onClick={handleExport}>Export CSV</AppButton>
        <AppButton variant="text" onClick={onClose}>Close</AppButton>
      </DialogActions>
    </Dialog>
  )
}
