import React from 'react'
import { Grid, Typography, Skeleton, Box, Chip, Card } from '@mui/material'
import GameCard from '../components/ui/GameCard/GameCard'
import AppButton from '../components/ui/AppButton'
import api from '../api'
import useAsyncData from '../hooks/useAsyncData'

export default function Games() {
  const { data, loading, error, refetch } = useAsyncData(
    () => api.get('/games').then(r => (Array.isArray(r.data) ? r.data : [])),
    []
  )
  const games = data ?? []

  if (error) {
    return (
      <Box sx={{ textAlign: 'center', py: 10 }}>
        <Typography variant="h5" gutterBottom>Couldn't load the games</Typography>
        <Typography color="text.secondary" sx={{ mb: 2 }}>Please check your connection and try again.</Typography>
        <AppButton onClick={refetch}>Retry</AppButton>
      </Box>
    )
  }

  if (loading && games.length === 0) {
    return (
      <Grid container spacing={3}>
        {[1, 2, 3, 4, 5, 6].map(i => (
          <Grid item xs={12} sm={6} md={4} key={i}>
            <Card>
              <Skeleton variant="rectangular" height={180} />
              <Box sx={{ p: 2 }}>
                <Skeleton width="60%" height={28} />
                <Skeleton width="40%" height={20} sx={{ mt: 1 }} />
              </Box>
            </Card>
          </Grid>
        ))}
      </Grid>
    )
  }

  if (games.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 10 }}>
        <Typography variant="h1" sx={{ fontSize: '3rem', mb: 1 }}>🎮</Typography>
        <Typography variant="h5" gutterBottom>No games yet</Typography>
        <Typography color="text.secondary">Check back soon — challenges are coming!</Typography>
      </Box>
    )
  }

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 3 }}>
        <Typography variant="h5" component="h1">Todays Challenges</Typography>
        <Chip
          label={`${games.length} game${games.length !== 1 ? 's' : ''}`}
          size="small"
          color="primary"
          sx={{ fontWeight: 700 }}
        />
      </Box>

      <Grid container spacing={3}>
        {games.map(game => (
          <Grid item xs={12} sm={6} md={4} key={game.id}>
            <GameCard game={game} />
          </Grid>
        ))}
      </Grid>
    </>
  )
}
