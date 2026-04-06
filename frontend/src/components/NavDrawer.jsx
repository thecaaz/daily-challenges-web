import React from 'react'
import { Drawer, Box, List, ListItemButton, ListItemIcon, ListItemText } from '@mui/material'
import { Link, useLocation } from 'react-router-dom'
import HomeIcon from '@mui/icons-material/Home'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings'

export default function NavDrawer({ open, onClose, user }) {
  const location = useLocation()

  const isActive = (to) => {
    if (to === '/') return location.pathname === '/'
    return location.pathname.startsWith(to)
  }

  return (
    <Drawer anchor="left" open={open} onClose={onClose}>
      <Box sx={{ width: 250 }} role="presentation" onClick={onClose} onKeyDown={onClose}>
        <List>
          <ListItemButton
            component={Link}
            to="/"
            selected={isActive('/')}
            sx={{ '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: (theme) => theme.palette.action.selected } }}
          >
            <ListItemIcon><HomeIcon /></ListItemIcon>
            <ListItemText primary="Home" />
          </ListItemButton>

          {user?.isAdmin && (
            <ListItemButton
              component={Link}
              to="/admin"
              selected={isActive('/admin')}
              sx={{ '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: (theme) => theme.palette.action.selected } }}
            >
              <ListItemIcon><AdminPanelSettingsIcon /></ListItemIcon>
              <ListItemText primary="Admin" />
            </ListItemButton>
          )}
        </List>
      </Box>
    </Drawer>
  )
}
