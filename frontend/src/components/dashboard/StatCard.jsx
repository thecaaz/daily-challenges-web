import React from 'react'
import { Paper, Avatar, Box, Typography } from '@mui/material'

export default function StatCard({ icon, label, value, color = 'primary' }) {
  return (
    <Paper
      elevation={0}
      sx={{
        px: 2.5, py: 1.5,
        borderRadius: 3,
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        border: '1px solid',
        borderColor: 'divider',
        bgcolor: 'background.paper',
        minWidth: 130,
      }}
    >
      <Avatar sx={{ width: 36, height: 36, bgcolor: `${color}.main` }}>{icon}</Avatar>
      <Box>
        <Typography variant="h6" fontWeight={700} lineHeight={1}>{value}</Typography>
        <Typography variant="caption" color="text.secondary">{label}</Typography>
      </Box>
    </Paper>
  )
}
