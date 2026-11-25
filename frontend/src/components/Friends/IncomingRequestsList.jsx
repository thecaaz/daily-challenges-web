import React from 'react'
import { Typography, Button, Paper, Chip } from '@mui/material'
import FriendListItem, { DividedList } from './FriendListItem'

export default function IncomingRequestsList({ requests, actions }) {
  return (
    <Paper sx={{ mb: 3 }}>
      <Typography variant="h6" sx={{ p: 2, pb: 0 }}>
        Incoming Requests <Chip label={requests.length} size="small" color="primary" sx={{ ml: 1 }} />
      </Typography>
      <DividedList
        items={requests}
        getKey={friendRequest => friendRequest.id}
        renderItem={friendRequest => (
          <FriendListItem userId={friendRequest.senderId} username={friendRequest.senderUsername}>
            <Button size="small" variant="contained" color="success" onClick={() => actions.accept(friendRequest)} disabled={actions.isRequestBusy(friendRequest)} sx={{ mr: 1 }}>
              Accept
            </Button>
            <Button size="small" variant="outlined" color="error" onClick={() => actions.decline(friendRequest)} disabled={actions.isRequestBusy(friendRequest)}>
              Decline
            </Button>
          </FriendListItem>
        )}
      />
    </Paper>
  )
}
