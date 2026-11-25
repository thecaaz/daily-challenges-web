import React from 'react'
import { Box, Typography, TextField, Button, CircularProgress, Paper } from '@mui/material'
import PersonAddIcon from '@mui/icons-material/PersonAdd'

/** `friendRequest` is the state returned by useSendFriendRequest. */
export default function AddFriendForm({ friendRequest }) {
  const { username, setUsername, isSending, canSend, send } = friendRequest
  const sendIcon = isSending ? <CircularProgress size={14} color="inherit" /> : <PersonAddIcon />

  const handleKeyDown = event => {
    if (event.key === 'Enter' && !isSending) send()
  }

  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Typography variant="h6" gutterBottom>Add Friend by Username</Typography>
      <Box sx={{ display: 'flex', gap: 1 }}>
        <TextField
          size="small"
          label="Username"
          value={username}
          onChange={event => setUsername(event.target.value)}
          onKeyDown={handleKeyDown}
          disabled={isSending}
          sx={{ flex: 1 }}
        />
        <Button variant="contained" startIcon={sendIcon} onClick={send} disabled={!canSend}>
          Send Request
        </Button>
      </Box>
    </Paper>
  )
}
