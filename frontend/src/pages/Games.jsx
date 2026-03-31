import React, { useEffect, useState } from 'react'
import { Grid, Typography, Skeleton, Box, Chip, Card } from '@mui/material'
import GameCard from '../components/ui/GameCard/GameCard'
import api from '../api'

export default function Games() {
  const [games, setGames] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => { fetchGames() }, [])

  const fetchGames = async () => {
    const res = await api.get('/games')
    setGames(res.data)
    setLoading(false)
  }

  if (loading) {
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
        <Typography variant="h5" component="h1">Today's Challenges</Typography>
        <Chip
          label={`${games.length} game${games.length !== 1 ? 's' : ''}`}
          size="small"
          color="primary"
          sx={{ fontWeight: 700 }}
        />
      </Box>

      <Grid container spacing={3}>
        {games.map(g => (
          <Grid item xs={12} sm={6} md={4} key={g.id}>
            <GameCard game={g} />
          </Grid>
        ))}
      </Grid>
    </>
  )
}
