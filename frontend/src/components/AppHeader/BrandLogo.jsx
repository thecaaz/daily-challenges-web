import React from 'react'
import { Link } from 'react-router-dom'
import { Box, Typography } from '@mui/material'

const logoLinkSx = {
  display: 'flex',
  alignItems: 'center',
  gap: 1.5,
  textDecoration: 'none',
  color: 'inherit',
  flexShrink: 0,
  mr: 2,
}

const logoIconSx = {
  fontSize: '1.6rem',
  lineHeight: 1,
  borderRadius: '10px',
  p: '6px',
  background: 'linear-gradient(180deg,#fff,#ffeef8)',
  boxShadow: '0 4px 12px rgba(255,122,182,0.15)',
}

/** App icon, name and tagline, linking home. */
export default function BrandLogo() {
  return (
    <Box component={Link} to="/" sx={logoLinkSx}>
      <Box sx={logoIconSx}>🎮</Box>
      <Box>
        <Typography variant="subtitle1" sx={{ fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 800, lineHeight: 1.1 }}>
          Daily Challenges
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1 }}>
          Play, compete, repeat
        </Typography>
      </Box>
    </Box>
  )
}
