import React, { useEffect, useState } from 'react'
import {
  Card, CardContent, CardMedia, CardActionArea, CardActions,
  Typography, Grid, Button, Skeleton, Box, Chip,
} from '@mui/material'
import SportsEsportsIcon from '@mui/icons-material/SportsEsports'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import { Link } from 'react-router-dom'
import api, { getApiRoot } from '../api'

export default function Games() {
  const [games, setGames] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => { fetchGames() }, [])

  const fetchGames = async () => {
    const res = await api.get('/games')
    setGames(res.data)
    setLoading(false)
  }

  const apiRoot = getApiRoot()

  if (loading) {
    return (
      <Grid container spacing={3}>
        {[1, 2, 3, 4, 5, 6].map(i => (
          <Grid item xs={12} sm={6} md={4} key={i}>
            <Card>
              <Skeleton variant="rectangular" height={180} />
              <CardContent>
                <Skeleton width="60%" height={28} />
                <Skeleton width="40%" height={20} sx={{ mt: 1 }} />
              </CardContent>
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
            <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
              <CardActionArea component={Link} to={`/games/${g.id}`} sx={{ flexGrow: 1 }}>
                {g.imageUrl ? (
                  <CardMedia
                    component="img"
                    image={`${apiRoot}${g.imageUrl}`}
                    alt={g.name}
                    height={180}
                    loading="lazy"
                    sx={{ objectFit: 'cover' }}
                  />
                ) : (
                  <Box
                    sx={{
                      height: 180,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      background: 'linear-gradient(135deg, rgba(255,122,182,0.12), rgba(255,209,102,0.12))',
                      fontSize: '3.5rem',
                    }}
                  >
                    🎮
                  </Box>
                )}
                <CardContent sx={{ pb: 1 }}>
                  <Typography variant="h6" gutterBottom sx={{ lineHeight: 1.3 }}>
                    {g.name}
                  </Typography>
                </CardContent>
              </CardActionArea>
              <CardActions sx={{ px: 2, pb: 2, pt: 0, gap: 1 }}>
                <Button
                  component={Link}
                  to={`/submit/${g.id}`}
                  variant="contained"
                  color="primary"
                  size="small"
                  startIcon={<EmojiEventsIcon />}
                >
                  Submit Score
                </Button>
                {g.url && (
                  <Button
                    href={g.url}
                    target="_blank"
                    rel="noreferrer"
                    variant="outlined"
                    color="primary"
                    size="small"
                    endIcon={<OpenInNewIcon />}
                  >
                    Play
                  </Button>
                )}
              </CardActions>
            </Card>
          </Grid>
        ))}
      </Grid>
    </>
  )
}
