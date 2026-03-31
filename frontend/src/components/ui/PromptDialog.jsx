import React from 'react'
import { Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, TextField } from '@mui/material'
import AppButton from './AppButton'

export default function PromptDialog({ open, title, message, label, value, onChange, confirmText = 'Confirm', cancelText = 'Cancel', onConfirm, onCancel }) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        {message && <DialogContentText sx={{ mb: 1 }}>{message}</DialogContentText>}
        <TextField
          autoFocus
          fullWidth
          label={label}
          value={value}
          onChange={e => onChange(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') onConfirm() }}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <AppButton variant="outlined" color="inherit" onClick={onCancel}>{cancelText}</AppButton>
        <AppButton onClick={onConfirm}>{confirmText}</AppButton>
      </DialogActions>
    </Dialog>
  )
}
