import React, { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { Box, Button, Paper, Stack, TextField, Typography } from '@mui/material'

export default function AuthForm({
  icon,
  title,
  subtitle,
  submitLabel = 'Submit',
  submitLoadingLabel = 'Submitting…',
  linkPrefix,
  linkText,
  linkTo,
  passwordAutoComplete = 'current-password',
  onSubmit,
}) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setLoading(true)
    try {
      if (onSubmit) await onSubmit(username, password)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
      <Paper elevation={0} sx={{ p: { xs: 3, sm: 5 }, width: '100%', maxWidth: 420 }}>
        <Box sx={{ textAlign: 'center', mb: 3 }}>
          <Typography variant="h1" sx={{ fontSize: '2.8rem', mb: 0.5 }}>{icon}</Typography>
          <Typography variant="h5" component="h1">{title}</Typography>
          {subtitle && <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{subtitle}</Typography>}
        </Box>

        <Box component="form" onSubmit={handleSubmit}>
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
              autoComplete={passwordAutoComplete}
            />
            <Button
              type="submit"
              variant="contained"
              color="primary"
              size="large"
              disabled={loading}
              sx={{ mt: 1 }}
            >
              {loading ? submitLoadingLabel : submitLabel}
            </Button>
          </Stack>
        </Box>

        {linkText && linkTo && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 3, textAlign: 'center' }}>
            {linkPrefix && <span>{linkPrefix} </span>}
            <RouterLink to={linkTo} style={{ color: '#ff7ab6', fontWeight: 600 }}>{linkText}</RouterLink>
          </Typography>
        )}
      </Paper>
    </Box>
  )
}
