import React from 'react'
import { Box, Tooltip, Typography } from '@mui/material'
import LockIcon from '@mui/icons-material/Lock'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import { achievementIcon } from '../../utils/achievementIcons'

export default function AchievementBadge({ achievement }) {
  const unlocked = !!achievement.unlockedAt || !!achievement.UnlockedAt
  const name = achievement.name || achievement.Name
  const description = achievement.description || achievement.Description
  const iconKey = achievement.iconKey || achievement.IconKey

  const unlockedAt = achievement.unlockedAt || achievement.UnlockedAt
  const tooltipContent = (
    <Box sx={{ maxWidth: 200 }}>
      <Typography variant="subtitle2">{name}</Typography>
      <Typography variant="body2" sx={{ opacity: 0.8 }}>{description}</Typography>
      {unlockedAt && (
        <Typography variant="caption" sx={{ opacity: 0.6 }}>
          {new Date(unlockedAt).toLocaleDateString()}
        </Typography>
      )}
    </Box>
  )

  return (
    <Tooltip title={tooltipContent} arrow>
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 0.5,
          p: 1,
          borderRadius: 2,
          border: '1px solid',
          borderColor: unlocked ? 'primary.main' : 'divider',
          bgcolor: unlocked ? 'action.selected' : 'action.disabledBackground',
          opacity: unlocked ? 1 : 0.45,
          cursor: 'default',
          minWidth: 72,
          userSelect: 'none',
        }}
      >
        <Box sx={{ fontSize: 28, lineHeight: 1 }}>
          {unlocked ? (achievementIcon(iconKey) || <EmojiEventsIcon />) : <LockIcon color="disabled" />}
        </Box>
        <Typography
          variant="caption"
          align="center"
          sx={{ fontSize: '0.65rem', lineHeight: 1.2, wordBreak: 'break-word', maxWidth: 64 }}
        >
          {name}
        </Typography>
      </Box>
    </Tooltip>
  )
}
