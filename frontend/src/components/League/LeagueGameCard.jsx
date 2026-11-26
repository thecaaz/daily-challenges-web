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
import { placeholderGradient } from '../../styles/tokens'

const ICON_SIZE = 40
const PLACEHOLDER_ICON_BACKGROUND = placeholderGradient(0.25)

const stopPropagation = (event) => event.stopPropagation()

/**
 * One game in a league's games tab. Clicking the card calls `onSelect(gameId)`;
 * clicking a recent-plays bar calls `onSelect(gameId, isoDate)` to jump to that
 * day's leaderboard.
 */
export default function LeagueGameCard({ summary, onSelect }) {
  const { gameId, lastPlayedAt, myBestScore, myRank, recentPlays, recentPlayDays } = summary
  const isTopRank = myRank === 1
  const hasRecentPlays = recentPlays && recentPlays.length > 0

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
          <GameTitle summary={summary} isTopRank={isTopRank} />
          <GameStatChips summary={summary} isTopRank={isTopRank} />
          <MyBestScore score={myBestScore} />
          {hasRecentPlays && (
            <Box sx={{ mt: 1 }} onClick={stopPropagation}>
              <RecentPlaysBar
                recentPlays={recentPlays}
                recentPlayDays={recentPlayDays}
                onDayClick={(isoDate) => onSelect(gameId, isoDate)}
              />
            </Box>
          )}
        </CardContent>
      </CardActionArea>
      <LastPlayed lastPlayedAt={lastPlayedAt} />
    </Card>
  )
}

function GameTitle({ summary, isTopRank }) {
  const { gameId, gameName, iconUrl } = summary

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1 }}>
      <GameIcon iconUrl={iconUrl} gameName={gameName} />
      <Typography
        variant="subtitle1"
        component={Link}
        to={`/games/${gameId}`}
        onClick={stopPropagation}
        sx={{ fontWeight: 600, lineHeight: 1.2, textDecoration: 'none', color: 'inherit', flexGrow: 1 }}
      >
        {gameName}
      </Typography>
      {isTopRank && (
        <EmojiEventsIcon fontSize="small" sx={{ color: 'warning.main', flexShrink: 0 }} />
      )}
    </Box>
  )
}

function GameIcon({ iconUrl, gameName }) {
  if (iconUrl) {
    return <Avatar src={imageUrl(iconUrl)} alt={gameName} variant="rounded" sx={{ width: ICON_SIZE, height: ICON_SIZE }} />
  }

  return (
    <Avatar
      variant="rounded"
      sx={{ width: ICON_SIZE, height: ICON_SIZE, background: PLACEHOLDER_ICON_BACKGROUND, fontSize: '1.25rem' }}
    >
      🎮
    </Avatar>
  )
}

function GameStatChips({ summary, isTopRank }) {
  const { myRank, topScore, playCount } = summary
  const hasRank = myRank != null
  const hasTopScore = topScore != null
  const playsLabel = `${formatNumber(playCount)} play${playCount !== 1 ? 's' : ''}`

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mb: 1 }}>
      {hasRank && (
        <Chip label={`#${myRank} rank`} size="small" color={isTopRank ? 'warning' : 'default'} sx={{ fontWeight: 600 }} />
      )}
      {hasTopScore && <Chip label={`Top: ${topScore}`} size="small" variant="outlined" />}
      <Chip label={playsLabel} size="small" variant="outlined" />
    </Box>
  )
}

function MyBestScore({ score }) {
  const hasPlayed = score != null

  return (
    <Typography variant="body2" color={hasPlayed ? 'text.primary' : 'text.disabled'}>
      My best: {hasPlayed ? score : 'Not played'}
    </Typography>
  )
}

function LastPlayed({ lastPlayedAt }) {
  return (
    <Box sx={{ px: 2, pb: 1.5 }}>
      <Typography variant="caption" color="text.secondary">
        {lastPlayedAt ? `Last played ${timeAgo(lastPlayedAt)}` : 'Not played recently'}
      </Typography>
    </Box>
  )
}
