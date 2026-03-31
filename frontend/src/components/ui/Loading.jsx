import React from 'react'
import { Box, CircularProgress, Typography } from '@mui/material'

export default function Loading({ message = 'Loading…' }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', p: 3 }}>
      <CircularProgress size={20} />
      <Typography sx={{ ml: 1, color: 'text.secondary' }}>{message}</Typography>
    </Box>
  )
}
