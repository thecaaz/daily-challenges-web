import React from 'react'
import { Paper, Box, Typography, Divider, List } from '@mui/material'

export default function Section({ title, icon, children, empty, emptyText = 'Nothing here yet.' }) {
  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 3,
        border: '1px solid',
        borderColor: 'divider',
        overflow: 'hidden',
        height: '100%',
      }}
    >
      <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1, bgcolor: 'background.default' }}>
        {icon}
        <Typography variant="subtitle1" fontWeight={700}>{title}</Typography>
      </Box>
      <Divider />
      {empty
        ? <Box sx={{ px: 2, py: 3, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">{emptyText}</Typography>
          </Box>
        : <List dense disablePadding sx={{ px: 0.5, py: 0.5 }}>{children}</List>
      }
    </Paper>
  )
}
