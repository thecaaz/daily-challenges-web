import React, { useState } from 'react'
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
import timeAgo from '../utils/timeAgo'
import { useAuth } from '../contexts/AuthContext'
import useNotifications from '../hooks/useNotifications'
import { winnerGold } from '../styles/tokens'

const MAX_BADGE_COUNT = 99
const LEAGUE_TYPE_PREFIX = 'league_'
const FRIEND_TYPES = ['friend_request', 'friend_request_accepted']

const isFriendType = type => FRIEND_TYPES.includes(type)
const isLeagueType = type => !!type?.startsWith(LEAGUE_TYPE_PREFIX)

const iconSx = { fontSize: '1.2rem' }
const winnerIconSx = { color: winnerGold, fontSize: '1.3rem' }
const rankChipSx = { fontWeight: 700, fontSize: '0.65rem', height: 20 }
const popoverPaperSx = { width: 340, maxHeight: 420 }
const headerSx = { p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }
const markAllReadSx = { textTransform: 'none', fontSize: '0.75rem' }
const listSx = { maxHeight: 340, overflow: 'auto' }

/**
 * Where a notification leads, by its `type`, or null if it has nowhere to go.
 * Types: day_win, day_placement, friend_request, friend_request_accepted,
 * league_invite, league_joined, achievement.
 */
export function notificationRoute(n, user) {
  const type = n?.type ?? ''
  if (isFriendType(type)) return '/friends'
  if (isLeagueType(type)) return '/leagues'
  if (type === 'achievement') return user?.id != null ? `/users/${user.id}` : null
  if (n?.gameId) {
    return n.scoringDay
      ? `/games/${n.gameId}?scoringDay=${encodeURIComponent(n.scoringDay)}`
      : `/games/${n.gameId}`
  }
  return null
}

function NotificationIcon({ n }) {
  if (n.type === 'day_win') return <EmojiEventsIcon sx={winnerIconSx} />
  if (n.rank != null) return <Chip label={`#${n.rank}`} size="small" sx={rankChipSx} />
  if (isFriendType(n.type)) return <PersonAddIcon color="action" sx={iconSx} />
  if (isLeagueType(n.type)) return <GroupsIcon color="action" sx={iconSx} />
  if (n.type === 'achievement') return <MilitaryTechIcon color="action" sx={iconSx} />
  return <NotificationsIcon color="action" sx={iconSx} />
}

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

  return (
    <>
      <IconButton color="inherit" onClick={handleOpen} size="small" sx={{ mr: 0.5 }}>
        <Badge badgeContent={unreadCount} color="error" max={MAX_BADGE_COUNT}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: popoverPaperSx } }}
      >
        <Box sx={headerSx}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Notifications</Typography>
          {unreadCount > 0 && (
            <AppButton variant="text" size="small" onClick={markAllRead} sx={markAllReadSx}>
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

function NotificationListItem({ notification, onClick, onRemove }) {
  const { isRead } = notification
  return (
    <ListItemButton
      onClick={onClick}
      sx={{
        bgcolor: isRead ? 'transparent' : 'action.hover',
        py: 1, px: 1.5,
        borderBottom: '1px solid',
        borderColor: 'divider',
        display: 'flex',
        alignItems: 'center'
      }}
    >
      <Box sx={{ mr: 1, flexShrink: 0 }}>
        <NotificationIcon n={notification} />
      </Box>

      <ListItemText
        primary={notification.message}
        secondary={timeAgo(notification.createdAt)}
        primaryTypographyProps={{ variant: 'body2', fontWeight: isRead ? 400 : 600, lineHeight: 1.3 }}
        secondaryTypographyProps={{ variant: 'caption' }}
        sx={{ mr: 1 }}
      />

      <IconButton aria-label="Remove notification" size="small" onClick={onRemove}>
        <DeleteOutlineIcon fontSize="small" />
      </IconButton>
    </ListItemButton>
  )
}
