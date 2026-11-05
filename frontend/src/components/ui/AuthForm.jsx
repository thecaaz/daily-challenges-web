import React, { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { Box, Paper, Stack, TextField, Typography } from '@mui/material'
import AppButton from './AppButton'

export const USERNAME_MIN = 3
export const USERNAME_MAX = 32
export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 128
const USERNAME_PATTERN = /^[A-Za-z0-9_.-]+$/

/** Server rules for a new account (Register). Returns { username?, password? } messages. */
export function validateCredentials(username, password) {
  const errors = {}
  const u = username.trim()
  if (u.length < USERNAME_MIN || u.length > USERNAME_MAX) {
    errors.username = `Username must be ${USERNAME_MIN}–${USERNAME_MAX} characters`
  } else if (!USERNAME_PATTERN.test(u)) {
    errors.username = 'Use only letters, digits, "_", "." and "-"'
  }
  if (password.length < PASSWORD_MIN || password.length > PASSWORD_MAX) {
    errors.password = `Password must be ${PASSWORD_MIN}–${PASSWORD_MAX} characters`
  }
  return errors
}

/**
 * Login/Register form. The username is trimmed before `onSubmit`.
 * `validate` (Register) enforces the server's username/password rules on the
 * client; Login only requires non-empty fields so existing accounts that
 * predate the rules can still sign in.
 */
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
  validate = false,
  onSubmit,
}) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [errors, setErrors] = useState({})

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (loading) return
    const trimmed = username.trim()
    const nextErrors = validate
      ? validateCredentials(trimmed, password)
      : {
          ...(trimmed ? {} : { username: 'Enter your username' }),
          ...(password ? {} : { password: 'Enter your password' }),
        }
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return
    setLoading(true)
    try {
      if (onSubmit) await onSubmit(trimmed, password)
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
              onChange={e => { setUsername(e.target.value); setErrors(prev => ({ ...prev, username: undefined })) }}
              required
              autoComplete="username"
              autoFocus
              error={!!errors.username}
              helperText={errors.username || (validate ? `${USERNAME_MIN}–${USERNAME_MAX} characters: letters, digits, _ . -` : undefined)}
              inputProps={validate ? { maxLength: USERNAME_MAX } : undefined}
            />
            <TextField
              label="Password"
              type="password"
              value={password}
              onChange={e => { setPassword(e.target.value); setErrors(prev => ({ ...prev, password: undefined })) }}
              required
              autoComplete={passwordAutoComplete}
              error={!!errors.password}
              helperText={errors.password || (validate ? `${PASSWORD_MIN}–${PASSWORD_MAX} characters` : undefined)}
              inputProps={validate ? { maxLength: PASSWORD_MAX } : undefined}
            />
            <AppButton
              type="submit"
              variant="contained"
              color="primary"
              size="large"
              disabled={loading}
              sx={{ mt: 1 }}
            >
              {loading ? submitLoadingLabel : submitLabel}
            </AppButton>
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
