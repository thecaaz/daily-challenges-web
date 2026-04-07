import React, { useEffect, useState } from 'react'
import { Grid, Typography, Skeleton, Box, Chip, Card } from '@mui/material'
import GameCard from '../components/ui/GameCard/GameCard'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import { useNavigate } from 'react-router-dom'

export default function Dashboard() {
  const { user, loading } = useAuth()
  const navigate = useNavigate()
  const [favorites, setFavorites] = useState([])
  const [others, setOthers] = useState([])
  const [loadingState, setLoadingState] = useState(true)

  useEffect(() => {
    if (!loading && !user) navigate('/login')
  }, [loading, user, navigate])

  useEffect(() => { if (user) fetchGames() }, [user])

  const fetchGames = async () => {
    try {
      const res = await api.get('/games')
      const all = Array.isArray(res.data) ? res.data : []
      const favs = all.filter(g => g.isFavorite)
      const othersList = all.filter(g => !g.isFavorite)
      setFavorites(favs)
      setOthers(othersList)
    } catch (err) {
      setFavorites([])
      setOthers([])
    } finally {
      setLoadingState(false)
    }
  }

  if (loading || loadingState) {
    return (
      <>
        <Grid container spacing={3} sx={{ mb: 4 }}>
          {[1, 2, 3].map(i => (
            <Grid item xs={12} sm={6} md={4} key={`fav-skel-${i}`}>
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

        <Grid container spacing={3}>
          {[1, 2, 3, 4].map(i => (
            <Grid item xs={12} sm={6} md={4} key={`other-skel-${i}`}>
              <Card>
                <Skeleton variant="rectangular" height={160} />
                <Box sx={{ p: 2 }}>
                  <Skeleton width="50%" height={26} />
                </Box>
              </Card>
            </Grid>
          ))}
        </Grid>
      </>
    )
  }

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 3 }}>
        <Typography variant="h5" component="h1">Dashboard</Typography>
      </Box>

      {/* Favorites section */}
      <Box sx={{ mb: 4 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2 }}>
          <Typography variant="h6">Favorites</Typography>
          <Chip label={`${favorites.length} favorite${favorites.length !== 1 ? 's' : ''}`} size="small" color="primary" sx={{ fontWeight: 700 }} />
        </Box>

        {favorites.length === 0 ? (
          <Box sx={{ textAlign: 'center', py: 4, bgcolor: 'background.paper', borderRadius: 1 }}>
            <Typography color="text.secondary">You have no favorites yet. Add games to your favorites to see them here.</Typography>
          </Box>
        ) : (
          <Grid container spacing={3}>
            {favorites.map(game => (
              <Grid item xs={12} sm={6} md={4} key={`fav-${game.id}`}>
                <GameCard game={game} />
              </Grid>
            ))}
          </Grid>
        )}
      </Box>

      {/* Other games section */}
      <Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2 }}>
          <Typography variant="h6">Other games</Typography>
          <Chip label={`${others.length} game${others.length !== 1 ? 's' : ''}`} size="small" color="secondary" sx={{ fontWeight: 700 }} />
        </Box>

        {others.length === 0 ? (
          <Box sx={{ textAlign: 'center', py: 4 }}>
            <Typography color="text.secondary">No other games available.</Typography>
          </Box>
        ) : (
          <Grid container spacing={3}>
            {others.map(game => (
              <Grid item xs={12} sm={6} md={4} key={`other-${game.id}`}>
                <GameCard game={game} />
              </Grid>
            ))}
          </Grid>
        )}
      </Box>
    </>
  )
}
