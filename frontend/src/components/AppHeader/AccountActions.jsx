import React from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Box, Typography, IconButton } from '@mui/material'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import LightModeIcon from '@mui/icons-material/LightMode'
import AppButton from '../ui/AppButton'
import NotificationBell from '../NotificationBell'
import { useAuth } from '../../contexts/AuthContext'
import { useThemeMode } from '../../contexts/ThemeContext'

export function GuestActions() {
  return (
    <Box sx={{ display: 'flex', gap: 1 }}>
      <AppButton to="/login" variant="outlined" color="primary" size="small">Login</AppButton>
      <AppButton to="/register" variant="contained" color="primary" size="small">Register</AppButton>
    </Box>
  )
}

/**
 * Theme toggle, notifications, profile link and logout for a logged-in user.
 * Logging out always lands on home: logout() clears the local session even
 * when the server call fails.
 */
export default function AccountActions({ user }) {
  const { logout } = useAuth()
  const { mode, toggleMode } = useThemeMode()
  const navigate = useNavigate()

  const handleLogout = async () => {
    try {
      await logout()
    } catch {
    } finally {
      navigate('/')
    }
  }

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
      <IconButton aria-label="Toggle color scheme" size="small" onClick={toggleMode} sx={{ color: 'inherit' }}>
        {mode === 'dark' ? <LightModeIcon /> : <DarkModeIcon />}
      </IconButton>
      <NotificationBell />
      <Box component={Link} to={`/users/${user.id}`} sx={{ textDecoration: 'none', color: 'inherit', mx: 1, display: { xs: 'none', sm: 'block' } }}>
        <Typography variant="body2" color="text.secondary">
          {user.username}
        </Typography>
      </Box>
      <AppButton variant="contained" color="primary" size="small" onClick={handleLogout}>
        Logout
      </AppButton>
    </Box>
  )
}
