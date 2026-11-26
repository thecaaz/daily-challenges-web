import React from 'react'
import { Dialog, DialogTitle, DialogContent, DialogActions, IconButton } from '@mui/material'
import CloseIcon from '@mui/icons-material/Close'
import AppButton from './ui/AppButton'
import formatNumber from '../utils/formatNumber'
import useXpEventAudit from '../hooks/useXpEventAudit'
import XpEventFilters from './AdminUsers/XpEventFilters'
import XpEventsTable from './AdminUsers/XpEventsTable'
import UserLink from './ui/UserLink'

export { XP_EVENT_TYPES } from './AdminUsers/XpEventFilters'
export { nextDay, buildFilterParams } from '../hooks/useXpEventAudit'

function AuditedUserName({ userId, username }) {
  if (!username) return `User ${userId}`
  return <UserLink userId={userId}>{username}</UserLink>
}

function XpEventsPager({ audit }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12 }}>
      <div>Showing {formatNumber(audit.items.length)} of {formatNumber(audit.totalCount)}</div>
      <div>
        <AppButton variant="text" disabled={!audit.hasPreviousPage} onClick={audit.goToPreviousPage}>Prev</AppButton>
        <AppButton variant="text" disabled={!audit.hasNextPage} onClick={audit.goToNextPage}>Next</AppButton>
      </div>
    </div>
  )
}

export default function AdminUserAuditModal({ open, onClose, userId, username }) {
  const audit = useXpEventAudit({ open, userId })

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="lg">
      <DialogTitle>
        XP Audit — <AuditedUserName userId={userId} username={username} />
        <IconButton aria-label="close" onClick={onClose} sx={{ position: 'absolute', right: 8, top: 8 }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <XpEventFilters audit={audit} />
        <XpEventsTable events={audit.items} />
        <XpEventsPager audit={audit} />
      </DialogContent>
      <DialogActions>
        <AppButton variant="text" onClick={audit.exportCsv}>Export CSV</AppButton>
        <AppButton variant="text" onClick={onClose}>Close</AppButton>
      </DialogActions>
    </Dialog>
  )
}
