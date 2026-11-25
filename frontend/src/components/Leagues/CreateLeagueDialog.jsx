import React from 'react'
import { Button, CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions, TextField } from '@mui/material'

const LEAGUE_NAME_MAX_LENGTH = 100

/** `createLeague` is the state returned by useCreateLeague. */
export default function CreateLeagueDialog({ createLeague }) {
  const { isOpen, close, name, setName, isCreating, canCreate, create } = createLeague

  const handleKeyDown = event => {
    if (event.key === 'Enter' && !isCreating) create()
  }

  return (
    <Dialog open={isOpen} onClose={close} maxWidth="xs" fullWidth>
      <DialogTitle>Create League</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          label="League name"
          fullWidth
          value={name}
          onChange={event => setName(event.target.value)}
          inputProps={{ maxLength: LEAGUE_NAME_MAX_LENGTH }}
          onKeyDown={handleKeyDown}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={close}>Cancel</Button>
        <Button onClick={create} disabled={!canCreate} variant="contained">
          {isCreating ? <CircularProgress size={20} /> : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
