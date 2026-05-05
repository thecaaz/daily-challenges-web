import React from 'react'
import { Drawer, Box, List } from '@mui/material'
import DashboardIcon from '@mui/icons-material/Dashboard'
import SportsEsportsIcon from '@mui/icons-material/SportsEsports'
import PeopleIcon from '@mui/icons-material/People'
import GroupsIcon from '@mui/icons-material/Groups'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings'
import ExtensionIcon from '@mui/icons-material/Extension'
import NavDrawerItem from './NavDrawerItem'

export default function NavDrawer({ open, onClose, user }) {
  return (
    <Drawer anchor="left" open={open} onClose={onClose}>
      <Box sx={{ width: 250 }} role="presentation" onClick={onClose} onKeyDown={onClose}>
        <List>
          <NavDrawerItem to="/" icon={<DashboardIcon />} primary="Home" />
          <NavDrawerItem to="/games" icon={<SportsEsportsIcon />} primary="Games" />
          {user && <NavDrawerItem to="/friends" icon={<PeopleIcon />} primary="Friends" />}
          {user && <NavDrawerItem to="/leagues" icon={<GroupsIcon />} primary="Leagues" />}
          {user?.isAdmin && <NavDrawerItem to="/admin" icon={<AdminPanelSettingsIcon />} primary="Admin" />}
          <NavDrawerItem to="/extension" icon={<ExtensionIcon />} primary="Extension" />
        </List>
      </Box>
    </Drawer>
  )
}
