import React from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { Box, Paper, Stack, TextField, Typography } from '@mui/material'
import { useTheme } from '@mui/material/styles'
import AppButton from './AppButton'
import useCredentialsForm from '../../hooks/useCredentialsForm'

const DEFAULT_SUBMIT_LABELS = { idle: 'Submit', loading: 'Submitting…' }

/**
 * Login/Register form layout. `credentials` sets the validator and field
 * hints/limits (see utils/credentialRules); the username is trimmed before
 * `onSubmit(username, password)`.
 *
 * @param {{ heading: { icon, title, subtitle? }, submitLabels?: { idle, loading },
 *   alternative?: { prefix?, text, to }, credentials: object, onSubmit: Function }} props
 */
export default function AuthForm({ heading, submitLabels = DEFAULT_SUBMIT_LABELS, alternative, credentials, onSubmit }) {
  const { values, errors, loading, handleUsernameChange, handlePasswordChange, handleSubmit } =
    useCredentialsForm(credentials.validate, onSubmit)
  const submitLabel = loading ? submitLabels.loading : submitLabels.idle

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', pt: 6 }}>
      <Paper elevation={0} sx={{ p: { xs: 3, sm: 5 }, width: '100%', maxWidth: 420 }}>
        <AuthHeading {...heading} />

        <Box component="form" onSubmit={handleSubmit}>
          <Stack spacing={2}>
            <TextField
              label="Username"
              value={values.username}
              onChange={handleUsernameChange}
              required
              autoComplete="username"
              autoFocus
              error={Boolean(errors.username)}
              helperText={errors.username || credentials.username.hint}
              inputProps={credentials.username.inputProps}
            />
            <TextField
              label="Password"
              type="password"
              value={values.password}
              onChange={handlePasswordChange}
              required
              autoComplete={credentials.password.autoComplete}
              error={Boolean(errors.password)}
              helperText={errors.password || credentials.password.hint}
              inputProps={credentials.password.inputProps}
            />
            <AppButton
              type="submit"
              variant="contained"
              color="primary"
              size="large"
              disabled={loading}
              sx={{ mt: 1 }}
            >
              {submitLabel}
            </AppButton>
          </Stack>
        </Box>

        {alternative && <AlternativeLink {...alternative} />}
      </Paper>
    </Box>
  )
}

function AuthHeading({ icon, title, subtitle }) {
  return (
    <Box sx={{ textAlign: 'center', mb: 3 }}>
      <Typography variant="h1" sx={{ fontSize: '2.8rem', mb: 0.5 }}>{icon}</Typography>
      <Typography variant="h5" component="h1">{title}</Typography>
      {subtitle && <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{subtitle}</Typography>}
    </Box>
  )
}

function AlternativeLink({ prefix, text, to }) {
  return (
    <Typography variant="body2" color="text.secondary" sx={{ mt: 3, textAlign: 'center' }}>
      {prefix && <span>{prefix} </span>}
      <AccentLink to={to}>{text}</AccentLink>
    </Typography>
  )
}

function AccentLink({ to, children }) {
  const theme = useTheme()
  return <RouterLink to={to} style={{ color: theme.palette.primary.main, fontWeight: 600 }}>{children}</RouterLink>
}
