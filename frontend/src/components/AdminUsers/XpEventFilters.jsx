import React from 'react'
import { TextField, MenuItem } from '@mui/material'
import AppButton from '../ui/AppButton'

export const XP_EVENT_TYPES = [
  { value: 'submission', label: 'Submission' },
  { value: 'day_win', label: 'Day Win' },
  { value: 'admin_adjustment', label: 'Admin Adjustment' },
  { value: 'submission_revoked', label: 'Submission Revoked' },
]

const shrunkLabel = { shrink: true }

/** Date range and event type filters; `audit` is the state returned by useXpEventAudit. */
export default function XpEventFilters({ audit }) {
  const { filters, setFilter, apply, reset } = audit

  return (
    <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
      <TextField label="From" type="date" size="small" value={filters.from} onChange={event => setFilter('from', event.target.value)} InputLabelProps={shrunkLabel} />
      <TextField label="To (inclusive)" type="date" size="small" value={filters.to} onChange={event => setFilter('to', event.target.value)} InputLabelProps={shrunkLabel} />
      <TextField select label="Event Type" size="small" value={filters.eventType} onChange={event => setFilter('eventType', event.target.value)} style={{ minWidth: 160 }}>
        <MenuItem value="">All</MenuItem>
        {XP_EVENT_TYPES.map(type => <MenuItem key={type.value} value={type.value}>{type.label}</MenuItem>)}
      </TextField>
      <AppButton variant="outlined" onClick={apply}>Apply</AppButton>
      <AppButton variant="text" onClick={reset}>Reset</AppButton>
    </div>
  )
}
