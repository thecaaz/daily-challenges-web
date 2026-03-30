import React, { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { Box, Button, Paper, Stack, TextField, Typography } from '@mui/material'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'

export default function Register() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const { register } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const submit = async (e) => {
    e.preventDefault()
    setLoading(true)
    try {
      await register(username, password)
      showSnackbar('Account created! Please log in.', 'success')
      navigate('/login')
    } catch (err) {
      showSnackbar('Registration failed — username may already be taken', 'error')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
      <Paper elevation={0} sx={{ p: { xs: 3, sm: 5 }, width: '100%', maxWidth: 420 }}>
        <Box sx={{ textAlign: 'center', mb: 3 }}>
          <Typography variant="h1" sx={{ fontSize: '2.8rem', mb: 0.5 }}>🏆</Typography>
          <Typography variant="h5" component="h1">Join the challenge!</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Create an account to submit scores and compete with others.
          </Typography>
        </Box>
        <Box component="form" onSubmit={submit}>
          <Stack spacing={2}>
            <TextField
              label="Username"
              value={username}
              onChange={e => setUsername(e.target.value)}
              required
              autoComplete="username"
              autoFocus
            />
            <TextField
              label="Password"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
              autoComplete="new-password"
            />
            <Button
              type="submit"
              variant="contained"
              color="primary"
              size="large"
              disabled={loading}
              sx={{ mt: 1 }}
            >
              {loading ? 'Creating account…' : 'Register'}
            </Button>
          </Stack>
        </Box>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 3, textAlign: 'center' }}>
          Already have an account?{' '}
          <Link to="/login" style={{ color: '#ff7ab6', fontWeight: 600 }}>Log in</Link>
        </Typography>
      </Paper>
    </Box>
  )
}
