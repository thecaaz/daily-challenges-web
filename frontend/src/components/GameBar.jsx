import React, { useState } from 'react'
import './gamebar.css'
import { AppBar, Toolbar, Box, IconButton } from '@mui/material'
import MenuIcon from '@mui/icons-material/Menu'
import NavDrawer from './NavDrawer'
import VersionBadge from './VersionBadge'
import BrandLogo from './AppHeader/BrandLogo'
import XpStatus from './AppHeader/XpStatus'
import AccountActions, { GuestActions } from './AppHeader/AccountActions'
import { useAuth } from '../contexts/AuthContext'

const isFocusNavigationKey = event => event?.type === 'keydown' && (event.key === 'Tab' || event.key === 'Shift')

export default function GameBar() {
  const { user } = useAuth()
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)

  const closeDrawer = event => {
    if (isFocusNavigationKey(event)) return
    setIsDrawerOpen(false)
  }

  return (
    <AppBar position="sticky" elevation={0}>
      <Toolbar sx={{ gap: 1, minHeight: { xs: 56, sm: 64 } }}>
        <IconButton edge="start" color="inherit" aria-label="open navigation" onClick={() => setIsDrawerOpen(true)} sx={{ mr: 1 }}>
          <MenuIcon />
        </IconButton>
        <NavDrawer open={isDrawerOpen} onClose={closeDrawer} user={user} />
        <BrandLogo />
        <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'center' }}>
          <VersionBadge />
        </Box>
        {user && <XpStatus user={user} />}
        {user ? <AccountActions user={user} /> : <GuestActions />}
      </Toolbar>
    </AppBar>
  )
}
