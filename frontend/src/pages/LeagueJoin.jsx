import React, { useEffect, useState } from 'react'
import { Box, Typography, CircularProgress, Button } from '@mui/material'
import { useParams, useNavigate } from 'react-router-dom'
import { useSnackbar } from '../contexts/SnackbarContext'
import { useAuth } from '../contexts/AuthContext'
import api from '../api'

export default function LeagueJoin() {
  const { token } = useParams()
  const { user, loading } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()
  const [status, setStatus] = useState('loading') // loading | success | error

  useEffect(() => {
    if (loading) return  // wait for auth to restore from refresh cookie
    if (!user) {
      navigate(`/login?redirect=/leagues/join/${token}`)
      return
    }

    api.post(`/leagues/join/${token}`)
      .then(res => {
        showSnackbar(`You joined "${res.data.name}"!`)
        navigate(`/leagues/${res.data.id}`)
      })
      .catch(err => {
        const msg = err?.response?.data?.title || err?.response?.data || 'Invalid or expired invite link.'
        showSnackbar(typeof msg === 'string' ? msg : 'Could not join league.', 'error')
        setStatus('error')
      })
  }, [token, user, loading, navigate, showSnackbar])

  if (status === 'loading') {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mt: 8, gap: 2 }}>
        <CircularProgress />
        <Typography>Joining league…</Typography>
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mt: 8, gap: 2 }}>
      <Typography variant="h6" color="error">Could not join league.</Typography>
      <Typography color="text.secondary">The invite link may be invalid or expired.</Typography>
      <Button variant="outlined" onClick={() => navigate('/leagues')}>
        My Leagues
      </Button>
    </Box>
  )
}
