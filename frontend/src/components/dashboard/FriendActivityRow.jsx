import React from 'react'
import { Box, Typography, ListItem, ListItemAvatar, Avatar, Chip, Tooltip } from '@mui/material'
import ExtensionIcon from '@mui/icons-material/Extension'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import { Link } from 'react-router-dom'
import FavoriteButton from '../ui/FavoriteButton'
import PlayButton from '../ui/PlayButton'
import useAdapter from '../../hooks/useAdapter'
import imageUrl from '../../utils/imageUrl'
import timeAgo from '../../utils/timeAgo'

export default function FriendActivityRow({ activity }) {
  const adapter = useAdapter(activity.gameUrl)

  const fakeGame = {
    id: activity.gameId,
    url: activity.gameUrl,
    name: activity.gameName,
    imageUrl: activity.gameImageUrl,
    isFavorite: activity.isFavorite,
    hasSubmittedForLatest: activity.hasSubmittedForLatest,
  }

  const latest = activity.recentSubmissions?.[0]

  const uniqueSubmissions = (() => {
    const arr = activity.recentSubmissions || []
    const seen = new Set()
    const out = []
    for (const s of arr) {
      const key = (s.username || '').toLowerCase()
      if (!seen.has(key)) {
        seen.add(key)
        out.push(s)
      }
    }
    return out
  })()

  return (
    <ListItem
      disableGutters
      sx={{
        py: 1,
        px: 1.5,
        borderRadius: 2,
        transition: 'background 0.15s',
        '&:hover': { bgcolor: 'action.hover' },
        gap: 1.5,
        alignItems: 'center',
      }}
    >
      <ListItemAvatar sx={{ minWidth: 44 }}>
        <Avatar
          src={activity.gameImageUrl ? imageUrl(activity.gameImageUrl) : undefined}
          variant="rounded"
          sx={{ width: 40, height: 40, bgcolor: 'secondary.dark', fontSize: '1.2rem' }}
        >
          {!activity.gameImageUrl && '🎮'}
        </Avatar>
      </ListItemAvatar>

      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <Link to={`/games/${activity.gameId}`} style={{ textDecoration: 'none', color: 'inherit' }}>
            <Typography variant="body2" fontWeight={600} noWrap sx={{ maxWidth: 140 }}>
              {activity.gameName}
            </Typography>
          </Link>
          {adapter && (
            <Tooltip title="Playable in browser via extension">
              <ExtensionIcon sx={{ fontSize: 15, color: 'primary.main', flexShrink: 0 }} />
            </Tooltip>
          )}
          {activity.hasSubmittedForLatest && (
            <Tooltip title="Already submitted today">
              <CheckCircleIcon sx={{ fontSize: 15, color: 'success.main', flexShrink: 0 }} />
            </Tooltip>
          )}
        </Box>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.4, mt: 0.25 }}>
          {uniqueSubmissions.slice(0, 4).map((s) => (
            <Chip
              key={s.userId ?? s.username}
              label={s.username}
              size="small"
              sx={{ height: 18, fontSize: '0.65rem' }}
            />
          ))}
          {latest && (
            <Typography variant="caption" color="text.secondary" sx={{ alignSelf: 'center', ml: 0.5 }}>
              {timeAgo(latest.submittedAt)}
            </Typography>
          )}
        </Box>
      </Box>

      <FavoriteButton
        game={fakeGame}
        size="small"
        sx={{ p: 0.5 }}
        activeIconSx={{ color: 'warning.main' }}
        inactiveIconSx={{ color: 'text.secondary' }}
      />

      <PlayButton game={fakeGame} size="small" adapter={adapter} sx={{ flexShrink: 0 }} />
    </ListItem>
  )
}
