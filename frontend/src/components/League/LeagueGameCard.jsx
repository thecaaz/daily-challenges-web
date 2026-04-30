import React from 'react'
import {
  Card, CardActionArea, CardContent, Box, Typography, Avatar, Chip
} from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import { Link } from 'react-router-dom'
import imageUrl from '../../utils/imageUrl'
import timeAgo from '../../utils/timeAgo'
import formatNumber from '../../utils/formatNumber'
import RecentPlaysBar from './RecentPlaysBar'

export default function LeagueGameCard({ summary, onSelect }) {
  const {
    gameId, gameName, iconUrl, lastPlayedAt, playCount,
    topScore, myBestScore, myRank, recentPlays,
  } = summary

  const isTopRank = myRank === 1

  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        borderColor: isTopRank ? 'warning.main' : undefined,
        borderWidth: isTopRank ? 2 : 1,
        transition: 'border-color 0.2s',
      }}
    >
      <CardActionArea
        onClick={() => onSelect(gameId)}
        sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', alignItems: 'stretch' }}
      >
        <CardContent sx={{ flexGrow: 1, pb: 1 }}>
          {/* Header: icon + name */}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1 }}>
            {iconUrl ? (
              <Avatar
                src={imageUrl(iconUrl)}
                alt={gameName}
                variant="rounded"
                sx={{ width: 40, height: 40 }}
              />
            ) : (
              <Avatar
                variant="rounded"
                sx={{
                  width: 40,
                  height: 40,
                  background: 'linear-gradient(135deg, rgba(255,122,182,0.25), rgba(255,209,102,0.25))',
                  fontSize: '1.25rem',
                }}
              >
                🎮
              </Avatar>
            )}
            <Typography
              variant="subtitle1"
              component={Link}
              to={`/games/${gameId}`}
              onClick={e => e.stopPropagation()}
              sx={{ fontWeight: 600, lineHeight: 1.2, textDecoration: 'none', color: 'inherit', flexGrow: 1 }}
            >
              {gameName}
            </Typography>
            {isTopRank && (
              <EmojiEventsIcon fontSize="small" sx={{ color: 'warning.main', flexShrink: 0 }} />
            )}
          </Box>

          {/* Stats chips */}
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mb: 1 }}>
            {myRank != null && (
              <Chip
                label={`#${myRank} rank`}
                size="small"
                color={isTopRank ? 'warning' : 'default'}
                sx={{ fontWeight: 600 }}
              />
            )}
            {topScore != null && (
              <Chip
                label={`Top: ${topScore}`}
                size="small"
                variant="outlined"
              />
            )}
            <Chip
              label={`${formatNumber(playCount)} play${playCount !== 1 ? 's' : ''}`}
              size="small"
              variant="outlined"
            />
          </Box>

          {/* My best score */}
          <Typography variant="body2" color={myBestScore != null ? 'text.primary' : 'text.disabled'}>
            My best: {myBestScore != null ? myBestScore : 'Not played'}
          </Typography>

          {/* Recent plays bar */}
          {recentPlays && recentPlays.length > 0 && (
            <Box sx={{ mt: 1 }}>
              <RecentPlaysBar recentPlays={recentPlays} days={recentPlays.length} />
            </Box>
          )}
        </CardContent>
      </CardActionArea>

      {/* Footer */}
      <Box sx={{ px: 2, pb: 1.5 }}>
        <Typography variant="caption" color="text.secondary">
          Last played {timeAgo(lastPlayedAt)}
        </Typography>
      </Box>
    </Card>
  )
}
