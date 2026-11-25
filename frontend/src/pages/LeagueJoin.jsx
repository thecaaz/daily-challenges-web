import React from 'react'
import { Box, Typography, CircularProgress, Button } from '@mui/material'
import { useParams, useNavigate } from 'react-router-dom'
import useJoinLeague from '../hooks/useJoinLeague'

const centeredColumnSx = { display: 'flex', flexDirection: 'column', alignItems: 'center', mt: 8, gap: 2 }

export default function LeagueJoin() {
  const { token } = useParams()
  const navigate = useNavigate()
  const { hasFailed } = useJoinLeague(token)

  if (!hasFailed) {
    return (
      <Box sx={centeredColumnSx}>
        <CircularProgress />
        <Typography>Joining league…</Typography>
      </Box>
    )
  }

  return (
    <Box sx={centeredColumnSx}>
      <Typography variant="h6" color="error">Could not join league.</Typography>
      <Typography color="text.secondary">The invite link may be invalid or expired.</Typography>
      <Button variant="outlined" onClick={() => navigate('/leagues')}>
        My Leagues
      </Button>
    </Box>
  )
}
