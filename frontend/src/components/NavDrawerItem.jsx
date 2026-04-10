import React from 'react'
import { ListItemButton, ListItemIcon, ListItemText } from '@mui/material'
import { Link, useLocation } from 'react-router-dom'

export default function NavDrawerItem({ to, icon, primary }) {
  const location = useLocation()

  const isActive = (to) => {
    if (to === '/') return location.pathname === '/'
    return location.pathname.startsWith(to)
  }

  return (
    <ListItemButton
      component={Link}
      to={to}
      selected={isActive(to)}
      sx={{ '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: (theme) => theme.palette.action.selected } }}
    >
      {icon ? <ListItemIcon>{icon}</ListItemIcon> : null}
      <ListItemText primary={primary} />
    </ListItemButton>
  )
}
