import React from 'react'
import { Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions } from '@mui/material'
import AppButton from './AppButton'

export default function ConfirmDialog({ open, title, message, confirmText = 'Delete', cancelText = 'Cancel', onConfirm, onCancel, confirmColor = 'error' }) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <AppButton variant="outlined" color="inherit" onClick={onCancel}>{cancelText}</AppButton>
        <AppButton color={confirmColor} onClick={onConfirm}>{confirmText}</AppButton>
      </DialogActions>
    </Dialog>
  )
}
