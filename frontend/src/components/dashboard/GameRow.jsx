import React from 'react'
import { Box, Typography, ListItem, ListItemAvatar, Avatar, Chip, Tooltip } from '@mui/material'
import ExtensionIcon from '@mui/icons-material/Extension'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import { Link } from 'react-router-dom'
import FavoriteButton from '../ui/FavoriteButton'
import PlayButton from '../ui/PlayButton'
import useAdapter from '../../hooks/useAdapter'
import imageUrl from '../../utils/imageUrl'

export default function GameRow({ game, badge, badgeLabel }) {
  const adapter = useAdapter(game?.url)

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
      {/* Game image */}
      <ListItemAvatar sx={{ minWidth: 44 }}>
        <Avatar
          src={game.imageUrl ? imageUrl(game.imageUrl) : undefined}
          variant="rounded"
          sx={{ width: 40, height: 40, bgcolor: 'primary.dark', fontSize: '1.2rem' }}
        >
          {!game.imageUrl && '🎮'}
        </Avatar>
      </ListItemAvatar>

      {/* Name + icons */}
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap' }}>
          <Link to={`/games/${game.id}`} style={{ textDecoration: 'none', color: 'inherit' }}>
            <Typography variant="body2" fontWeight={600} noWrap sx={{ maxWidth: 160 }}>
              {game.name}
            </Typography>
          </Link>
          {adapter && (
            <Tooltip title="Playable in browser via extension">
              <ExtensionIcon sx={{ fontSize: 15, color: 'primary.main', flexShrink: 0 }} />
            </Tooltip>
          )}
          {game.hasSubmittedForLatest && (
            <Tooltip title="Already submitted today">
              <CheckCircleIcon sx={{ fontSize: 15, color: 'success.main', flexShrink: 0 }} />
            </Tooltip>
          )}
        </Box>
        {badge !== undefined && (
          <Chip
            label={badgeLabel}
            size="small"
            color="primary"
            variant="outlined"
            sx={{ height: 18, fontSize: '0.65rem', mt: 0.25 }}
          />
        )}
      </Box>

      {/* Favourite */}
      <FavoriteButton
        game={game}
        size="small"
        sx={{ p: 0.5 }}
        activeIconSx={{ color: 'warning.main' }}
        inactiveIconSx={{ color: 'text.secondary' }}
      />

      {/* Play */}
      <PlayButton game={game} size="small" adapter={adapter} sx={{ flexShrink: 0 }} />
    </ListItem>
  )
}
