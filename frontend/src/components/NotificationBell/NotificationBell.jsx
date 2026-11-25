import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { IconButton, Badge, Popover, List, Typography, Box, Divider } from '@mui/material'
import NotificationsIcon from '@mui/icons-material/Notifications'
import AppButton from '../ui/AppButton'
import { useAuth } from '../../contexts/AuthContext'
import useNotifications from '../../hooks/useNotifications'
import { notificationRoute } from './notificationTypes'
import NotificationListItem from './NotificationListItem'

const MAX_BADGE_COUNT = 99

const bellButtonSx = { mr: 0.5 }
const popoverAnchorOrigin = { vertical: 'bottom', horizontal: 'right' }
const popoverTransformOrigin = { vertical: 'top', horizontal: 'right' }
const popoverSlotProps = { paper: { sx: { width: 340, maxHeight: 420 } } }
const headerSx = { p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }
const titleSx = { fontWeight: 700 }
const markAllReadSx = { textTransform: 'none', fontSize: '0.75rem' }
const emptySx = { p: 3, textAlign: 'center' }
const listSx = { maxHeight: 340, overflow: 'auto' }

export default function NotificationBell() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [anchorEl, setAnchorEl] = useState(null)
  const { unreadCount, notifications, loadRecent, markRead, markAllRead, remove } = useNotifications(user)

  const handleOpen = (e) => {
    setAnchorEl(e.currentTarget)
    loadRecent()
  }

  const handleClose = () => setAnchorEl(null)

  const handleClickNotification = async (n) => {
    await markRead(n)
    handleClose()
    const route = notificationRoute(n, user)
    if (route) navigate(route)
  }

  const handleRemoveNotification = (e, n) => {
    e.stopPropagation()
    remove(n)
  }

  if (!user) return null

  const hasUnread = unreadCount > 0
  const isEmpty = notifications.length === 0

  return (
    <>
      <IconButton color="inherit" onClick={handleOpen} size="small" sx={bellButtonSx}>
        <Badge badgeContent={unreadCount} color="error" max={MAX_BADGE_COUNT}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={popoverAnchorOrigin}
        transformOrigin={popoverTransformOrigin}
        slotProps={popoverSlotProps}
      >
        <Box sx={headerSx}>
          <Typography variant="subtitle2" sx={titleSx}>Notifications</Typography>
          {hasUnread && (
            <AppButton variant="text" size="small" onClick={markAllRead} sx={markAllReadSx}>
              Mark all read
            </AppButton>
          )}
        </Box>
        <Divider />

        {isEmpty ? (
          <Box sx={emptySx}>
            <Typography variant="body2" color="text.secondary">No notifications yet</Typography>
          </Box>
        ) : (
          <List dense disablePadding sx={listSx}>
            {notifications.map(n => (
              <NotificationListItem
                key={n.id}
                notification={n}
                onClick={() => handleClickNotification(n)}
                onRemove={(e) => handleRemoveNotification(e, n)}
              />
            ))}
          </List>
        )}
      </Popover>
    </>
  )
}
