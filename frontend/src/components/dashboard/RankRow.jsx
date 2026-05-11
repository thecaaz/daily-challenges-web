import React from 'react'
import { Box, Typography, ListItem, ListItemAvatar, Avatar, Chip } from '@mui/material'
import { Link } from 'react-router-dom'
import imageUrl from '../../utils/imageUrl'

export default function RankRow({ rank }) {
  const medal = rank.rank === 1 ? '🥇' : rank.rank === 2 ? '🥈' : rank.rank === 3 ? '🥉' : null
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
          src={rank.gameImageUrl ? imageUrl(rank.gameImageUrl) : undefined}
          variant="rounded"
          sx={{ width: 40, height: 40, bgcolor: 'primary.dark', fontSize: '1.2rem' }}
        >
          {!rank.gameImageUrl && '🎮'}
        </Avatar>
      </ListItemAvatar>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Link to={`/games/${rank.gameId}`} style={{ textDecoration: 'none', color: 'inherit' }}>
          <Typography variant="body2" fontWeight={600} noWrap>{rank.gameName}</Typography>
        </Link>
        <Typography variant="caption" color="text.secondary">Score: {rank.score}</Typography>
      </Box>
      <Chip
        label={`${medal ? medal + ' ' : ''}#${rank.rank} / ${rank.totalSubmissions}`}
        size="small"
        color={rank.rank === 1 ? 'warning' : rank.rank <= 3 ? 'success' : 'default'}
        sx={{ fontWeight: 700, flexShrink: 0 }}
      />
    </ListItem>
  )
}
