import React from 'react'
import {
  Box, Typography, Table, TableBody, TableCell, TableHead, TableRow,
} from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import { Link } from 'react-router-dom'
import imageUrl from '../../utils/imageUrl'
import UserLink from './UserLink'

const TROPHY_COLOR = 'gold'
const HIDDEN_RANK = '–'

/**
 * Ranked league entries for one game and day. An entry's score, rank and
 * screenshot are masked when the day's scores are hidden or it has no score.
 */
export default function LeagueLeaderboardTable({ leaderboard, currentUserId }) {
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>#</TableCell>
          <TableCell>Player</TableCell>
          <TableCell>Score</TableCell>
          <TableCell>Screenshot</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {leaderboard.entries.map(entry => (
          <LeaderboardRow
            key={entry.userId}
            entry={entry}
            isHidden={leaderboard.scoresHidden || entry.score == null}
            isCurrentUser={entry.userId === currentUserId}
          />
        ))}
      </TableBody>
    </Table>
  )
}

function LeaderboardRow({ entry, isHidden, isCurrentUser }) {
  const isWinner = entry.rank === 1
  const hasScreenshot = !isHidden && entry.screenshotUrl && entry.submissionId != null

  return (
    <TableRow sx={isCurrentUser ? { bgcolor: 'action.selected' } : {}}>
      <TableCell>
        {isHidden && HIDDEN_RANK}
        {!isHidden && isWinner && <EmojiEventsIcon fontSize="small" sx={{ color: TROPHY_COLOR, verticalAlign: 'middle' }} />}
        {!isHidden && !isWinner && entry.rank}
      </TableCell>
      <TableCell>
        <UserLink userId={entry.userId}>{entry.username}</UserLink>
      </TableCell>
      <TableCell>
        {isHidden
          ? <Typography variant="body2" color="text.secondary" component="span">Hidden</Typography>
          : entry.score}
      </TableCell>
      <TableCell sx={{ py: 0.5 }}>
        {hasScreenshot
          ? <ScreenshotThumbnail entry={entry} />
          : <Typography variant="caption" color="text.disabled">—</Typography>}
      </TableCell>
    </TableRow>
  )
}

function ScreenshotThumbnail({ entry }) {
  return (
    <Link to={`/submission/${entry.submissionId}`}>
      <Box
        component="img"
        src={imageUrl(entry.screenshotUrl)}
        alt={`${entry.username} screenshot`}
        loading="lazy"
        sx={{
          height: 48,
          width: 'auto',
          maxWidth: 80,
          borderRadius: 1,
          objectFit: 'cover',
          display: 'block',
          cursor: 'pointer',
          '&:hover': { opacity: 0.85 },
        }}
      />
    </Link>
  )
}
