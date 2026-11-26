import React from 'react'
import { Typography, Button, Paper } from '@mui/material'
import FriendListItem, { DividedList } from './FriendListItem'

export default function SentRequestsList({ requests, actions }) {
  return (
    <Paper sx={{ mb: 3 }}>
      <Typography variant="h6" sx={{ p: 2, pb: 0 }}>Sent Requests</Typography>
      <DividedList
        items={requests}
        getKey={friendRequest => friendRequest.id}
        renderItem={friendRequest => (
          <FriendListItem userId={friendRequest.receiverId} username={friendRequest.receiverUsername} secondary="Pending">
            <Button size="small" variant="outlined" onClick={() => actions.cancel(friendRequest)} disabled={actions.isRequestBusy(friendRequest)}>
              Cancel
            </Button>
          </FriendListItem>
        )}
      />
    </Paper>
  )
}
