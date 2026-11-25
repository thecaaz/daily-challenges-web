import React from 'react'
import {
  Button, CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions, TextField,
} from '@mui/material'

/** Invite a user to the league by username. `invite` is the result of `useInviteByUsername`. */
export default function InviteUserDialog({ leagueName, invite }) {
  const canSend = !invite.isSending && invite.username.trim() !== ''

  const handleKeyDown = e => {
    if (e.key === 'Enter' && !invite.isSending) invite.send()
  }

  return (
    <Dialog open={invite.isOpen} onClose={invite.close} maxWidth="xs" fullWidth>
      <DialogTitle>Invite to {leagueName}</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          label="Username"
          fullWidth
          value={invite.username}
          onChange={e => invite.setUsername(e.target.value)}
          onKeyDown={handleKeyDown}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={invite.close}>Cancel</Button>
        <Button onClick={invite.send} disabled={!canSend} variant="contained">
          {invite.isSending ? <CircularProgress size={20} /> : 'Send Invite'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
