import React from 'react'
import {
  Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Typography,
} from '@mui/material'
import { useSnackbar } from '../../contexts/SnackbarContext'

/** Shows a freshly created invite link. `inviteLink` is the result of `useInviteLink`. */
export default function InviteLinkDialog({ inviteLink }) {
  const { showSnackbar } = useSnackbar()

  const copyLink = () => {
    Promise.resolve()
      .then(() => navigator.clipboard.writeText(inviteLink.link))
      .then(() => showSnackbar('Link copied!'))
      .catch(() => showSnackbar('Could not copy the link. Select it and copy it manually.', 'error'))
  }

  return (
    <Dialog open={inviteLink.isOpen} onClose={inviteLink.close} maxWidth="sm" fullWidth>
      <DialogTitle>Invite Link</DialogTitle>
      <DialogContent>
        <Typography variant="body2" sx={{ mb: 1 }}>
          This is a single-use link for one person: the first person who opens it joins the league,
          then it stops working. It expires in 7 days. Create a new link for each person you want to invite.
        </Typography>
        <TextField
          fullWidth
          value={inviteLink.link}
          InputProps={{ readOnly: true }}
          onClick={e => e.target.select()}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={copyLink}>Copy</Button>
        <Button onClick={inviteLink.close}>Close</Button>
      </DialogActions>
    </Dialog>
  )
}
