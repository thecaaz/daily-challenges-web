import React, { useEffect, useState } from 'react'
import { Grid, Typography, Skeleton, Box, Chip, Card, IconButton, Collapse } from '@mui/material'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import GameCard from '../components/ui/GameCard/GameCard'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import { useNavigate } from 'react-router-dom'

export default function Dashboard() {
  const { user, loading } = useAuth()
  const navigate = useNavigate()
  const [favorites, setFavorites] = useState([])
  const [others, setOthers] = useState([])
  const [otherOpen, setOtherOpen] = useState(false)
  const [loadingState, setLoadingState] = useState(true)

  useEffect(() => {
    if (!loading && !user) navigate('/login')
  }, [loading, user, navigate])

  useEffect(() => { if (user) fetchGames() }, [user])

  // Keep dashboard in sync when favorites are toggled elsewhere (optimistic)
  useEffect(() => {
    const sortAscById = (arr) => arr.slice().sort((a, b) => Number(a.id) - Number(b.id))

    const handler = (ev) => {
      const detail = ev?.detail || {}
      const gameId = detail.gameId
      const isFav = detail.isFavorite
      if (typeof gameId === 'undefined') return

      if (isFav) {
        // Move from others -> favorites (update others first, then favorites sorted)
        let movedHandled = false
        setOthers(prevOthers => {
          const idx = prevOthers.findIndex(g => String(g.id) === String(gameId))
          if (idx === -1) return prevOthers
          const moved = prevOthers[idx]
          movedHandled = true
          const newOthers = prevOthers.filter(g => String(g.id) !== String(gameId))
          // add to favorites in sorted order
          setFavorites(prevFavs => sortAscById([{ ...moved, isFavorite: true }, ...prevFavs]))
          return newOthers
        })

        if (!movedHandled) {
          // If not present in lists, fetch single game and merge then sort
          (async () => {
            try {
              const res = await api.get(`/games/${gameId}`)
              if (res && res.data) {
                setFavorites(f => {
                  if (f.some(x => String(x.id) === String(gameId))) return f
                  return sortAscById([{ ...res.data, isFavorite: true }, ...f])
                })
              }
            } catch (e) {
              // ignore
            }
          })()
        }
      } else {
        // Move from favorites -> others (update favorites first, then others sorted)
        setFavorites(prevFavs => {
          const idx = prevFavs.findIndex(g => String(g.id) === String(gameId))
          if (idx === -1) {
            // ensure others flag updated if present
            setOthers(prevOthers => prevOthers.map(g => String(g.id) === String(gameId) ? { ...g, isFavorite: false } : g))
            return prevFavs
          }
          const moved = prevFavs[idx]
          const newFavs = prevFavs.filter(g => String(g.id) !== String(gameId))
          setOthers(prev => {
            if (prev.some(g => String(g.id) === String(gameId))) return sortAscById(prev.map(g => String(g.id) === String(gameId) ? { ...g, isFavorite: false } : g))
            return sortAscById([{ ...moved, isFavorite: false }, ...prev])
          })
          return newFavs
        })
      }
    }

    window.addEventListener('favorite-changed', handler)
    return () => window.removeEventListener('favorite-changed', handler)
  }, [])

  const fetchGames = async () => {
    try {
      const res = await api.get('/games')
      const all = Array.isArray(res.data) ? res.data : []
      const favs = all.filter(g => g.isFavorite)
      const othersList = all.filter(g => !g.isFavorite)
      // Ensure ordering by id descending
      const sortAscById = (arr) => arr.slice().sort((a, b) => Number(a.id) - Number(b.id))
      setFavorites(sortAscById(favs))
      setOthers(sortAscById(othersList))
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
          <Box
            sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2, justifyContent: 'space-between', cursor: 'pointer' }}
            role="button"
            tabIndex={0}
            aria-expanded={otherOpen}
            onClick={() => setOtherOpen(o => !o)}
            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setOtherOpen(o => !o) } }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
              <Typography variant="h6">Other games</Typography>
              <Chip label={`${others.length} game${others.length !== 1 ? 's' : ''}`} size="small" color="secondary" sx={{ fontWeight: 700 }} />
            </Box>
            <IconButton size="small" aria-expanded={otherOpen} aria-label={otherOpen ? 'Collapse other games' : 'Expand other games'} onClick={(e) => { e.stopPropagation(); setOtherOpen(o => !o) }}>
              {otherOpen ? <ExpandLessIcon /> : <ExpandMoreIcon />}
            </IconButton>
          </Box>

          <Collapse in={otherOpen} timeout="auto" unmountOnExit>
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
          </Collapse>
      </Box>
    </>
  )
}
