import React from 'react'
import {
  Button, CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions, TextField,
} from '@mui/material'

const MAX_LEAGUE_NAME_LENGTH = 100

/** `rename` is the result of `useRenameLeague`. */
export default function RenameLeagueDialog({ rename }) {
  const canSave = !rename.isSaving && rename.name.trim() !== ''

  const handleKeyDown = e => {
    if (e.key === 'Enter' && !rename.isSaving) rename.save()
  }

  return (
    <Dialog open={rename.isOpen} onClose={rename.close} maxWidth="xs" fullWidth>
      <DialogTitle>Rename League</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          label="New name"
          fullWidth
          value={rename.name}
          onChange={e => rename.setName(e.target.value)}
          inputProps={{ maxLength: MAX_LEAGUE_NAME_LENGTH }}
          onKeyDown={handleKeyDown}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={rename.close}>Cancel</Button>
        <Button onClick={rename.save} disabled={!canSave} variant="contained">
          {rename.isSaving ? <CircularProgress size={20} /> : 'Rename'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
