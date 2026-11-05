import React, { useCallback, useEffect, useRef, useState } from 'react'
import { Dialog, DialogTitle, DialogContent, DialogActions, Table, TableHead, TableRow, TableCell, TableBody, TextField, MenuItem, IconButton, Box } from '@mui/material'
import { Link } from 'react-router-dom'
import AppButton from './ui/AppButton'
import CloseIcon from '@mui/icons-material/Close'
import api from '../api'
import { formatDateTimeUtc } from '../utils/dateFormat'
import { useSnackbar } from '../contexts/SnackbarContext'
import downloadBlob from '../utils/downloadBlob'
import formatNumber from '../utils/formatNumber'
import { getApiErrorMessage } from '../utils/apiError'

export const XP_EVENT_TYPES = [
  { value: 'submission', label: 'Submission' },
  { value: 'day_win', label: 'Day Win' },
  { value: 'admin_adjustment', label: 'Admin Adjustment' },
  { value: 'submission_revoked', label: 'Submission Revoked' },
]

/** "yyyy-MM-dd" -> the next day as "yyyy-MM-dd" (calendar arithmetic, timezone-free). */
export function nextDay(yyyyMmDd) {
  const d = new Date(`${yyyyMmDd}T00:00:00Z`)
  if (Number.isNaN(d.getTime())) return null
  d.setUTCDate(d.getUTCDate() + 1)
  return d.toISOString().slice(0, 10)
}

/**
 * Query params for the XP-event endpoints. The "To" date picked in the UI is
 * inclusive, while the API's `to` is exclusive, so the day after is sent.
 */
export function buildFilterParams({ from, to, eventType }) {
  const params = {}
  if (from) params.from = from
  if (to) {
    const end = nextDay(to)
    if (end) params.to = end
  }
  if (eventType) params.eventType = eventType
  return params
}

/** scoringDay is a "yyyy-MM-dd" string (older backends sent a full DateTime). */
function formatScoringDay(value) {
  if (!value) return ''
  const s = String(value)
  return /^\d{4}-\d{2}-\d{2}/.test(s) ? s.slice(0, 10) : s
}

const EMPTY_FILTERS = { from: '', to: '', eventType: '' }

export default function AdminUserAuditModal({ open, onClose, userId, username }) {
  const { showSnackbar } = useSnackbar()

  const [items, setItems] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [eventType, setEventType] = useState('')
  // Filters used for the rows on screen; paging reuses them so editing a
  // field without pressing Apply doesn't change what Prev/Next show.
  const [appliedFilters, setAppliedFilters] = useState(EMPTY_FILTERS)
  const requestRef = useRef(0)

  const fetchPage = useCallback(async (p, filters) => {
    if (!userId) return
    const requestId = ++requestRef.current
    try {
      const params = { page: p, pageSize, ...buildFilterParams(filters) }
      const res = await api.get(`/admin/users/${userId}/xp-events`, { params })
      if (requestId !== requestRef.current) return
      setItems(res.data?.items || [])
      setTotalCount(res.data?.totalCount || 0)
      setPage(p)
      setAppliedFilters(filters)
    } catch (err) {
      if (requestId !== requestRef.current) return
      showSnackbar(getApiErrorMessage(err, 'Failed to load XP events'), 'error')
    }
  }, [userId, pageSize, showSnackbar])

  // Opening the modal (or switching user) starts from a clean slate.
  useEffect(() => {
    if (!open) return
    setFrom('')
    setTo('')
    setEventType('')
    setItems([])
    setTotalCount(0)
    fetchPage(1, EMPTY_FILTERS)
  }, [open, fetchPage])

  const apply = () => fetchPage(1, { from, to, eventType })

  const reset = () => {
    setFrom('')
    setTo('')
    setEventType('')
    fetchPage(1, EMPTY_FILTERS)
  }

  const handleExport = async () => {
    try {
      // Export what the table shows (the applied filters).
      const params = buildFilterParams(appliedFilters)
      const res = await api.get(`/admin/users/${userId}/xp-events/export`, { params, responseType: 'blob' })
      downloadBlob(`xp-events-user-${userId}.csv`, new Blob([res.data]))
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to export CSV'), 'error')
    }
  }

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="lg">
      <DialogTitle>
        XP Audit — {username ? (
          <Box component={Link} to={`/users/${userId}`} sx={{ textDecoration: 'none', color: 'inherit' }}>{username}</Box>
        ) : `User ${userId}`}
        <IconButton aria-label="close" onClick={onClose} sx={{ position: 'absolute', right: 8, top: 8 }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
          <TextField label="From" type="date" size="small" value={from} onChange={e => setFrom(e.target.value)} InputLabelProps={{ shrink: true }} />
          <TextField label="To (inclusive)" type="date" size="small" value={to} onChange={e => setTo(e.target.value)} InputLabelProps={{ shrink: true }} />
          <TextField select label="Event Type" size="small" value={eventType} onChange={e => setEventType(e.target.value)} style={{ minWidth: 160 }}>
            <MenuItem value="">All</MenuItem>
            {XP_EVENT_TYPES.map(t => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
          </TextField>
          <AppButton variant="outlined" onClick={apply}>Apply</AppButton>
          <AppButton variant="text" onClick={reset}>Reset</AppButton>
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
                <TableCell>{formatNumber(it.amount)}</TableCell>
                <TableCell>{it.eventType}</TableCell>
                <TableCell style={{ maxWidth: 300, whiteSpace: 'pre-wrap' }}>{it.details}</TableCell>
                <TableCell>{it.submissionId ?? ''}</TableCell>
                <TableCell>{it.gameId ?? ''}</TableCell>
                <TableCell>{formatScoringDay(it.scoringDay)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12 }}>
          <div>Showing {formatNumber(items.length)} of {formatNumber(totalCount)}</div>
          <div>
            <AppButton variant="text" disabled={page <= 1} onClick={() => fetchPage(page - 1, appliedFilters)}>Prev</AppButton>
            <AppButton variant="text" disabled={page * pageSize >= totalCount} onClick={() => fetchPage(page + 1, appliedFilters)}>Next</AppButton>
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
