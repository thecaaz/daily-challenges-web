import React from 'react'
import { IconButton, ListItemButton, ListItemText, Box } from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import timeAgo from '../../utils/timeAgo'
import NotificationIcon from './NotificationIcon'

const READ_FONT_WEIGHT = 400
const UNREAD_FONT_WEIGHT = 600

const iconBoxSx = { mr: 1, flexShrink: 0 }
const textSx = { mr: 1 }
const secondaryTypographyProps = { variant: 'caption' }

export default function NotificationListItem({ notification, onClick, onRemove }) {
  const { isRead } = notification
  return (
    <ListItemButton onClick={onClick} sx={itemSxFor(isRead)}>
      <Box sx={iconBoxSx}>
        <NotificationIcon n={notification} />
      </Box>

      <ListItemText
        primary={notification.message}
        secondary={timeAgo(notification.createdAt)}
        primaryTypographyProps={primaryTypographyPropsFor(isRead)}
        secondaryTypographyProps={secondaryTypographyProps}
        sx={textSx}
      />

      <IconButton aria-label="Remove notification" size="small" onClick={onRemove}>
        <DeleteOutlineIcon fontSize="small" />
      </IconButton>
    </ListItemButton>
  )
}

function itemSxFor(isRead) {
  return {
    bgcolor: isRead ? 'transparent' : 'action.hover',
    py: 1, px: 1.5,
    borderBottom: '1px solid',
    borderColor: 'divider',
    display: 'flex',
    alignItems: 'center'
  }
}

function primaryTypographyPropsFor(isRead) {
  return { variant: 'body2', fontWeight: isRead ? READ_FONT_WEIGHT : UNREAD_FONT_WEIGHT, lineHeight: 1.3 }
}
