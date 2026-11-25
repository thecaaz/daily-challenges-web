import React from 'react'
import { Typography, Button, List, ListItem, ListItemText, ListItemSecondaryAction, Divider, Paper } from '@mui/material'

/** `response` is the state returned by useLeagueInvitationResponse. */
export default function PendingInvitationsList({ invitations, response }) {
  return (
    <Paper variant="outlined" sx={{ mb: 3, p: 2 }}>
      <Typography variant="subtitle1" gutterBottom fontWeight={600}>
        Pending Invitations
      </Typography>
      <List disablePadding>
        {invitations.map((invitation, index) => (
          <React.Fragment key={invitation.id}>
            {index > 0 && <Divider />}
            <ListItem disablePadding sx={{ py: 1 }}>
              <ListItemText primary={invitation.leagueName} secondary={`Invited by ${invitation.inviterUsername}`} />
              <ListItemSecondaryAction>
                <Button size="small" variant="contained" onClick={() => response.accept(invitation)} disabled={response.isResponding} sx={{ mr: 1 }}>
                  Accept
                </Button>
                <Button size="small" onClick={() => response.decline(invitation)} disabled={response.isResponding}>
                  Decline
                </Button>
              </ListItemSecondaryAction>
            </ListItem>
          </React.Fragment>
        ))}
      </List>
    </Paper>
  )
}
