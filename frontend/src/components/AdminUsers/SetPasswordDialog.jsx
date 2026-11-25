import React from 'react'
import { Dialog, DialogTitle, DialogContent, DialogActions, TextField } from '@mui/material'
import AppButton from '../ui/AppButton'

/** `passwordDialog` is the state returned by useSetPasswordDialog. */
export default function SetPasswordDialog({ passwordDialog }) {
  const { isOpen, close, password, setPassword, error, submit } = passwordDialog

  return (
    <Dialog open={isOpen} onClose={close}>
      <DialogTitle>Set Password</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          label="New Password"
          type="password"
          fullWidth
          value={password}
          onChange={event => setPassword(event.target.value)}
          error={!!error}
          helperText={error}
          style={{ marginTop: 8 }}
        />
      </DialogContent>
      <DialogActions>
        <AppButton variant="text" onClick={close}>Cancel</AppButton>
        <AppButton variant="contained" onClick={submit}>Confirm</AppButton>
      </DialogActions>
    </Dialog>
  )
}
