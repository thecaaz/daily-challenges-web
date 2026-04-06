import React from 'react'
import { Drawer, Box, List } from '@mui/material'
import HomeIcon from '@mui/icons-material/Home'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings'
import NavDrawerItem from './NavDrawerItem'

export default function NavDrawer({ open, onClose, user }) {
  return (
    <Drawer anchor="left" open={open} onClose={onClose}>
      <Box sx={{ width: 250 }} role="presentation" onClick={onClose} onKeyDown={onClose}>
        <List>
          <NavDrawerItem to="/" icon={<HomeIcon />} primary="Home" />
          {user?.isAdmin && <NavDrawerItem to="/admin" icon={<AdminPanelSettingsIcon />} primary="Admin" />}
        </List>
      </Box>
    </Drawer>
  )
}
