import React, { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Box, CircularProgress, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api from '../api'

export default function Play() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        const res = await api.get(`/games/${gameId}`)
        if (mounted) setGame(res.data)
      } catch (err) {
        console.error('Failed to load game', err)
      } finally {
        if (mounted) setLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [gameId])

  if (loading) return <Box sx={{ mt: 6, display: 'flex', justifyContent: 'center' }}><CircularProgress /></Box>

  if (!game) return <Typography sx={{ mt: 4 }}>Game not found.</Typography>

  const url = game.url

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h5">{game.name}</Typography>
        {url && (
          <AppButton href={url} target="_blank" rel="noreferrer" variant="outlined">Open in new tab</AppButton>
        )}
      </Box>

      {!url ? (
        <Typography>No playable URL for this game.</Typography>
      ) : (
        <Box sx={{ width: '100vw', marginLeft: 'calc(50% - 50vw)', height: '80vh' }}>
          <iframe src={url} title={game.name || 'Play'} style={{ width: '100%', height: '100%', border: 0 }} />
        </Box>
      )}
    </Box>
  )
}
