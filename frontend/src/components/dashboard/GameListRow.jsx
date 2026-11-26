import React from 'react'
import { Box, Typography, ListItem, ListItemAvatar, Avatar, Tooltip } from '@mui/material'
import ExtensionIcon from '@mui/icons-material/Extension'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import { Link } from 'react-router-dom'
import FavoriteButton from '../ui/FavoriteButton'
import PlayButton from '../ui/PlayButton'
import useAdapter from '../../hooks/useAdapter'
import imageUrl from '../../utils/imageUrl'

const rowSx = {
  py: 1,
  px: 1.5,
  borderRadius: 2,
  transition: 'background 0.15s',
  '&:hover': { bgcolor: 'action.hover' },
  gap: 1.5,
  alignItems: 'center',
}

/**
 * Dashboard list row for a game: avatar, linked name with extension and
 * "submitted" icons, `children` below the name, then favourite and play buttons.
 *
 * @param {{ game: { id, name, url, imageUrl, isFavorite, hasSubmittedForLatest },
 *   look: { avatarColor: string, titleMaxWidth: number, titleRowSx?: object } }} props
 */
export default function GameListRow({ game, look, children }) {
  const adapter = useAdapter(game?.url)

  return (
    <ListItem disableGutters sx={rowSx}>
      <GameAvatar game={game} color={look.avatarColor} />

      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, ...look.titleRowSx }}>
          <Link to={`/games/${game.id}`} style={{ textDecoration: 'none', color: 'inherit' }}>
            <Typography variant="body2" fontWeight={600} noWrap sx={{ maxWidth: look.titleMaxWidth }}>
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
        {children}
      </Box>

      <FavoriteButton
        game={game}
        sx={{ p: 0.5 }}
        activeIconSx={{ color: 'warning.main' }}
        inactiveIconSx={{ color: 'text.secondary' }}
      />

      <PlayButton game={game} size="small" adapter={adapter} sx={{ flexShrink: 0 }} />
    </ListItem>
  )
}

function GameAvatar({ game, color }) {
  const hasImage = Boolean(game.imageUrl)

  return (
    <ListItemAvatar sx={{ minWidth: 44 }}>
      <Avatar
        src={hasImage ? imageUrl(game.imageUrl) : undefined}
        variant="rounded"
        sx={{ width: 40, height: 40, bgcolor: color, fontSize: '1.2rem' }}
      >
        {!hasImage && '🎮'}
      </Avatar>
    </ListItemAvatar>
  )
}
