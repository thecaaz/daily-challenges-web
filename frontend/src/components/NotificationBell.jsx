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
import api from '../api'
import timeAgo from '../utils/timeAgo'
import { useAuth } from '../contexts/AuthContext'

const POLL_INTERVAL = 30000 // 30 seconds

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
    if (n.gameId && n.scoringDay) {
      navigate(`/games/${n.gameId}?scoringDay=${n.scoringDay}`)
    }
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
    try {
      setNotifications(prev => prev.filter(x => x.id !== n.id))
      if (!n.isRead) setUnreadCount(prev => Math.max(0, prev - 1))
      await api.delete(`/notifications/${n.id}`)
    } catch {
      // ignore
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
                  {n.type === 'day_win'
                    ? <EmojiEventsIcon sx={{ color: '#FFD700', fontSize: '1.3rem' }} />
                    : <Chip label={`#${n.rank ?? '—'}`} size="small" sx={{ fontWeight: 700, fontSize: '0.65rem', height: 20 }} />
                  }
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
