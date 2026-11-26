import React from 'react'
import { Button } from '@mui/material'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import LinkIcon from '@mui/icons-material/Link'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'

export default function LeagueOwnerActions({ onInvite, onCreateLink, isCreatingLink, onRename, onDelete }) {
  return (
    <>
      <Button size="small" startIcon={<PersonAddIcon />} onClick={onInvite}>
        Invite
      </Button>
      <Button size="small" startIcon={<LinkIcon />} onClick={onCreateLink} disabled={isCreatingLink}>
        Create invite link
      </Button>
      <Button size="small" onClick={onRename}>
        Rename
      </Button>
      <Button size="small" color="error" startIcon={<DeleteOutlineIcon />} onClick={onDelete}>
        Delete
      </Button>
    </>
  )
}
