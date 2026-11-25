import React from 'react'
import { Paper, IconButton, Typography, Box } from '@mui/material'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import PlayButton from '../ui/PlayButton'
import AppButton from '../ui/AppButton'
import toLocalResetTime from '../../utils/toLocalResetTime'

/** The games an admin can edit, delete or moderate submissions for. */
export default function AdminGameList({ games, onEdit, onDelete, onManageSubmissions }) {
  return (
    <>
      <Typography variant="h5" sx={{ mt: 3, mb: 1 }}>Manage Games</Typography>
      <div>
        {games.map(game => (
          <AdminGameRow
            key={game.id}
            game={game}
            onEdit={onEdit}
            onDelete={onDelete}
            onManageSubmissions={onManageSubmissions}
          />
        ))}
      </div>
    </>
  )
}

function AdminGameRow({ game, onEdit, onDelete, onManageSubmissions }) {
  const hasPlayLink = !!game.url
  const hasResetTime = !!game.resetTime

  return (
    <Paper sx={{ p: 1.5, mb: 1.5 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <strong>{game.name}</strong> {hasPlayLink && <PlayButton game={game}><span style={{ textDecoration: 'none' }}>(link)</span></PlayButton>}
          {hasResetTime && (
            <Typography variant="caption" color="text.secondary" component="div">
              Resets at {toLocalResetTime(game.resetTime)} local ({game.resetTime} UTC)
            </Typography>
          )}
        </div>
        <div>
          <IconButton onClick={() => onEdit(game)} title="Edit" aria-label={`Edit ${game.name}`}><EditIcon /></IconButton>
          <IconButton onClick={() => onDelete(game.id)} title="Delete" aria-label={`Delete ${game.name}`}><DeleteIcon /></IconButton>
          <AppButton onClick={() => onManageSubmissions(game.id)} sx={{ ml: 1 }}>Manage Submissions</AppButton>
        </div>
      </Box>
    </Paper>
  )
}
