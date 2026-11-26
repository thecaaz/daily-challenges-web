import React from 'react'
import { Grid, Typography } from '@mui/material'
import UserGameStatCard from './UserGameStatCard'

export default function TopGamesList({ games = [] }) {
  if (!games || games.length === 0) return <Typography color="text.secondary">No top games yet.</Typography>

  return (
    <Grid container spacing={2}>
      {games.map(game => (
        <Grid item xs={12} sm={6} md={4} key={game.gameId}>
          <UserGameStatCard game={game} />
        </Grid>
      ))}
    </Grid>
  )
}
