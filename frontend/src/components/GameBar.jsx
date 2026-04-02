import React, { useRef, useState, useEffect } from 'react'
import './gamebar.css'
import { Link, useNavigate } from 'react-router-dom'
import { AppBar, Toolbar, Box, Typography, Chip, LinearProgress, Tooltip, IconButton } from '@mui/material'
import AppButton from './ui/AppButton'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import LightModeIcon from '@mui/icons-material/LightMode'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings'
import LocalFireDepartmentIcon from '@mui/icons-material/LocalFireDepartment'
import { useAuth } from '../contexts/AuthContext'
import { useThemeMode } from '../contexts/ThemeContext'
import NotificationBell from './NotificationBell'
import VersionBadge from './VersionBadge'

export default function GameBar() {
  const { user, logout, fetchMe } = useAuth()
  const { mode, toggleMode } = useThemeMode()
  const navigate = useNavigate()

  // Real XP data from the authenticated user object
  const level = user?.level ?? 1
  const xpInto = user?.xpIntoLevel ?? 0
  const xpToNext = user?.xpToNextLevel ?? 100
  const xpForLevel = xpInto + xpToNext
  const streak = user?.streak ?? 0
  const progress = xpForLevel > 0 ? Math.min(100, Math.round((xpInto / xpForLevel) * 100)) : 0

  // Flash "+N XP" whenever totalXp increases
  const prevXpRef = useRef(null)
  const gainTimerRef = useRef(null)
  const [gainDisplay, setGainDisplay] = useState(null)

  useEffect(() => {
    if (!user) { prevXpRef.current = null; return }
    const current = user.totalXp ?? 0
    if (prevXpRef.current !== null && current > prevXpRef.current) {
      const gained = current - prevXpRef.current
      setGainDisplay(gained)
      clearTimeout(gainTimerRef.current)
      gainTimerRef.current = setTimeout(() => setGainDisplay(null), 2500)
    }
    prevXpRef.current = current
    return () => clearTimeout(gainTimerRef.current)
  }, [user?.totalXp])

  // Re-fetch on level-up so xpIntoLevel / xpToNextLevel are accurate
  const prevLevelRef = useRef(null)
  useEffect(() => {
    if (!user) { prevLevelRef.current = null; return }
    const currentLevel = user.level ?? 1
    if (prevLevelRef.current !== null && currentLevel > prevLevelRef.current) {
      fetchMe()
    }
    prevLevelRef.current = currentLevel
  }, [user?.level])

  return (
    <AppBar position="sticky" elevation={0}>
      <Toolbar sx={{ gap: 1, minHeight: { xs: 56, sm: 64 } }}>
        {/* Logo + title */}
        <Box
          component={Link}
          to="/"
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            textDecoration: 'none',
            color: 'inherit',
            flexShrink: 0,
            mr: 2,
          }}
        >
          <Box
            sx={{
              fontSize: '1.6rem',
              lineHeight: 1,
              borderRadius: '10px',
              p: '6px',
              background: 'linear-gradient(180deg,#fff,#ffeef8)',
              boxShadow: '0 4px 12px rgba(255,122,182,0.15)',
            }}
          >
            🎮
          </Box>
          <Box>
            <Typography
              variant="subtitle1"
              sx={{ fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 800, lineHeight: 1.1 }}
            >
              Daily Challenges
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1 }}>
              Play, compete, repeat
            </Typography>
          </Box>
        </Box>

        {/* Spacer (center area) */}
        <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'center' }}>
          <VersionBadge />
        </Box>

        {/* XP bar (only when logged in) */}
        {user && (
          <Box className="xp" sx={{ minWidth: { xs: 140, sm: 220 }, mr: 1 }}>
            <Box className="xp-bar-wrapper">
              <Tooltip
                title={`${xpInto.toLocaleString()} / ${xpForLevel.toLocaleString()} XP`}
                placement="bottom"
              >
                <LinearProgress
                  variant="determinate"
                  value={progress}
                  aria-label={`XP progress: ${xpInto} of ${xpForLevel}`}
                />
              </Tooltip>
              {gainDisplay && (
                <span className="xp-gain" key={gainDisplay + Date.now()}>+{gainDisplay} XP</span>
              )}
            </Box>
            <Box
              className="xp-meta"
              sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: '4px', flexWrap: 'wrap' }}
            >
              <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'none', sm: 'inline-flex' } }}>
                Level <Box component="strong" sx={{ ml: 0.5, fontWeight: 700 }}>{level}</Box> &nbsp;•&nbsp; {xpInto.toLocaleString()}/{xpForLevel.toLocaleString()} XP
              </Typography>

              <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'inline-flex', sm: 'none' } }}>
                Lvl <Box component="strong" sx={{ ml: 0.5, fontWeight: 700 }}>{level}</Box>
              </Typography>

              {streak > 1 && (
                <Chip
                  icon={<LocalFireDepartmentIcon sx={{ fontSize: '0.9rem !important' }} />}
                  label={streak}
                  size="small"
                  color="secondary"
                  sx={{ height: 18, fontSize: '0.7rem', fontWeight: 700, ml: 0.5 }}
                />
              )}
            </Box>
          </Box>
        )}

        {/* Nav actions */}
        {user ? (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <IconButton aria-label="Toggle color scheme" size="small" onClick={toggleMode} sx={{ color: 'inherit' }}>
              {mode === 'dark' ? <LightModeIcon /> : <DarkModeIcon />}
            </IconButton>
            <NotificationBell />
            {user.isAdmin && (
              <AppButton to="/admin" variant="outlined" color="primary" size="small" startIcon={<AdminPanelSettingsIcon />}>
                Admin
              </AppButton>
            )}
            <Box component={Link} to={`/users/${user.id}`} sx={{ textDecoration: 'none', color: 'inherit', mx: 1, display: { xs: 'none', sm: 'block' } }}>
              <Typography variant="body2" color="text.secondary">
                {user.username}
              </Typography>
            </Box>
            <AppButton variant="contained" color="primary" size="small" onClick={() => { logout(); navigate('/') }}>
              Logout
            </AppButton>
          </Box>
        ) : (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <AppButton to="/login" variant="outlined" color="primary" size="small">Login</AppButton>
            <AppButton to="/register" variant="contained" color="primary" size="small">Register</AppButton>
          </Box>
        )}
      </Toolbar>
    </AppBar>
  )
}


