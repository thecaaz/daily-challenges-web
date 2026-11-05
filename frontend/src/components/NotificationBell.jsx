import React, { useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  IconButton, Badge, Popover, List, ListItemButton, ListItemText,
  Typography, Box, Divider, Chip
} from '@mui/material'
import AppButton from './ui/AppButton'
import NotificationsIcon from '@mui/icons-material/Notifications'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import GroupsIcon from '@mui/icons-material/Groups'
import MilitaryTechIcon from '@mui/icons-material/MilitaryTech'
import api from '../api'
import timeAgo from '../utils/timeAgo'
import { useAuth } from '../contexts/AuthContext'

const POLL_INTERVAL = 30000 // 30 seconds

/**
 * Where a notification leads, by its `type`, or null if it has nowhere to go.
 * Types: day_win, day_placement, friend_request, friend_request_accepted,
 * league_invite, league_joined, achievement.
 */
export function notificationRoute(n, user) {
  const type = n?.type ?? ''
  if (type === 'friend_request' || type === 'friend_request_accepted') return '/friends'
  if (type.startsWith('league_')) return '/leagues'
  if (type === 'achievement') return user?.id != null ? `/users/${user.id}` : null
  if (n?.gameId) {
    return n.scoringDay
      ? `/games/${n.gameId}?scoringDay=${encodeURIComponent(n.scoringDay)}`
      : `/games/${n.gameId}`
  }
  return null
}

function NotificationIcon({ n }) {
  if (n.type === 'day_win') return <EmojiEventsIcon sx={{ color: '#FFD700', fontSize: '1.3rem' }} />
  if (n.rank != null) {
    return <Chip label={`#${n.rank}`} size="small" sx={{ fontWeight: 700, fontSize: '0.65rem', height: 20 }} />
  }
  if (n.type === 'friend_request' || n.type === 'friend_request_accepted') return <PersonAddIcon color="action" sx={{ fontSize: '1.2rem' }} />
  if (n.type?.startsWith('league_')) return <GroupsIcon color="action" sx={{ fontSize: '1.2rem' }} />
  if (n.type === 'achievement') return <MilitaryTechIcon color="action" sx={{ fontSize: '1.2rem' }} />
  return <NotificationsIcon color="action" sx={{ fontSize: '1.2rem' }} />
}

export default function NotificationBell() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [unreadCount, setUnreadCount] = useState(0)
  const [notifications, setNotifications] = useState([])
  const [anchorEl, setAnchorEl] = useState(null)
  const pollRef = useRef(null)

  const fetchUnreadCount = useCallback(async () => {
    if (!user) return
    try {
      const res = await api.get('/notifications/unread-count')
      setUnreadCount(res.data.unreadCount ?? 0)
    } catch {
      // silently ignore
    }
  }, [user])

  // Poll unread count
  useEffect(() => {
    if (!user) {
      setUnreadCount(0)
      return
    }
    fetchUnreadCount()
    pollRef.current = setInterval(fetchUnreadCount, POLL_INTERVAL)
    return () => clearInterval(pollRef.current)
  }, [user, fetchUnreadCount])

  const fetchNotifications = async () => {
    try {
      const res = await api.get('/notifications?page=1&pageSize=10')
      setNotifications(res.data.items ?? [])
      setUnreadCount(res.data.unreadCount ?? 0)
    } catch {
      // silently ignore
    }
  }

  const handleOpen = (e) => {
    setAnchorEl(e.currentTarget)
    fetchNotifications()
  }

  const handleClose = () => setAnchorEl(null)

  const handleClickNotification = async (n) => {
    if (!n.isRead) {
      try {
        await api.post(`/notifications/${n.id}/read`)
        setUnreadCount(prev => Math.max(0, prev - 1))
        setNotifications(prev => prev.map(x => x.id === n.id ? { ...x, isRead: true } : x))
      } catch { /* ignore */ }
    }
    handleClose()

    const route = notificationRoute(n, user)
    if (route) navigate(route)
  }

  const handleMarkAllRead = async () => {
    try {
      await api.post('/notifications/read-all')
      setUnreadCount(0)
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })))
    } catch { /* ignore */ }
  }

  const handleDeleteNotification = async (e, n) => {
    e.stopPropagation()
    // Optimistic removal; put it back where it was if the delete fails.
    const index = notifications.findIndex(x => x.id === n.id)
    setNotifications(prev => prev.filter(x => x.id !== n.id))
    if (!n.isRead) setUnreadCount(prev => Math.max(0, prev - 1))
    try {
      await api.delete(`/notifications/${n.id}`)
    } catch {
      setNotifications(prev => {
        if (prev.some(x => x.id === n.id)) return prev
        const next = [...prev]
        next.splice(index >= 0 ? Math.min(index, next.length) : next.length, 0, n)
        return next
      })
      if (!n.isRead) setUnreadCount(prev => prev + 1)
    }
  }

  const open = Boolean(anchorEl)

  if (!user) return null

  return (
    <>
      <IconButton color="inherit" onClick={handleOpen} size="small" sx={{ mr: 0.5 }}>
        <Badge badgeContent={unreadCount} color="error" max={99}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 340, maxHeight: 420 } } }}
      >
        <Box sx={{ p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Notifications</Typography>
          {unreadCount > 0 && (
            <AppButton variant="text" size="small" onClick={handleMarkAllRead} sx={{ textTransform: 'none', fontSize: '0.75rem' }}>
              Mark all read
            </AppButton>
          )}
        </Box>
        <Divider />

        {notifications.length === 0 ? (
          <Box sx={{ p: 3, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">No notifications yet</Typography>
          </Box>
        ) : (
          <List dense disablePadding sx={{ maxHeight: 340, overflow: 'auto' }}>
            {notifications.map(n => (
              <ListItemButton
                key={n.id}
                onClick={() => handleClickNotification(n)}
                sx={{
                  bgcolor: n.isRead ? 'transparent' : 'action.hover',
                  py: 1, px: 1.5,
                  borderBottom: '1px solid',
                  borderColor: 'divider',
                  display: 'flex',
                  alignItems: 'center'
                }}
              >
                <Box sx={{ mr: 1, flexShrink: 0 }}>
                  <NotificationIcon n={n} />
                </Box>

                <ListItemText
                  primary={n.message}
                  secondary={timeAgo(n.createdAt)}
                  primaryTypographyProps={{ variant: 'body2', fontWeight: n.isRead ? 400 : 600, lineHeight: 1.3 }}
                  secondaryTypographyProps={{ variant: 'caption' }}
                  sx={{ mr: 1 }}
                />

                <IconButton
                  aria-label="Remove notification"
                  size="small"
                  onClick={(e) => handleDeleteNotification(e, n)}
                >
                  <DeleteOutlineIcon fontSize="small" />
                </IconButton>
              </ListItemButton>
            ))}
          </List>
        )}
      </Popover>
    </>
  )
}
