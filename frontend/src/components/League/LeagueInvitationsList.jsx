import React from 'react'
import {
  Typography, List, ListItem, ListItemText, IconButton, Divider, Tooltip,
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'

export default function LeagueInvitationsList({ invitations, onCancel }) {
  const pendingInvitations = invitations || []

  if (pendingInvitations.length === 0) {
    return <Typography color="text.secondary">No pending invitations.</Typography>
  }

  return (
    <List disablePadding>
      {pendingInvitations.map((invitation, index) => (
        <React.Fragment key={invitation.id}>
          {index > 0 && <Divider />}
          <ListItem disablePadding sx={{ py: 0.5 }}>
            <ListItemText
              primary={invitation.inviteeUsername ?? 'Link invite'}
              secondary={`Sent by ${invitation.inviterUsername}`}
            />
            <Tooltip title="Cancel invitation">
              <IconButton size="small" onClick={() => onCancel(invitation.id)}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </ListItem>
        </React.Fragment>
      ))}
    </List>
  )
}
