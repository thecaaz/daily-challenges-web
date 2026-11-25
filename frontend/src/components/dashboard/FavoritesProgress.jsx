import React from 'react'
import { Box, Typography, Paper, LinearProgress } from '@mui/material'
import StarIcon from '@mui/icons-material/Star'

/** How many of today's favorite games the user has already submitted. */
export default function FavoritesProgress({ submittedCount, total }) {
  const percentDone = total > 0 ? (submittedCount / total) * 100 : 0
  const isAllDone = submittedCount === total

  return (
    <Paper elevation={0} sx={{ p: 2, mb: 2, borderRadius: 3, border: '1px solid', borderColor: 'divider' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 1, gap: 1 }}>
        <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
        <Typography variant="subtitle2" fontWeight={700}>Today's Favorites</Typography>
        <Box sx={{ flex: 1 }} />
        <Typography variant="body2" color="text.secondary">
          {submittedCount} / {total} done
        </Typography>
      </Box>
      <LinearProgress
        variant="determinate"
        value={percentDone}
        color={isAllDone ? 'success' : 'primary'}
        sx={{ borderRadius: 2, height: 8 }}
      />
    </Paper>
  )
}
