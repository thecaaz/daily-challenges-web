import React from 'react'
import { Card, CardContent, Typography, Box, Link as MuiLink } from '@mui/material'
import { Link } from 'react-router-dom'
import timeAgo from '../../utils/timeAgo'
import formatNumber from '../../utils/formatNumber'
import ScoreSparkline from './ScoreSparkline'

export default function UserGameStatCard({ game }) {
  const { gameId, highestScore, bestSubmissionId, lastPlayed, scoreHistory, rankingMode } = game
  const name = game.name ?? 'Game'
  const plays = game.plays ?? 0

  return (
    <Card variant="outlined">
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
          <MuiLink component={Link} to={`/games/${gameId}`} underline="hover">
            <Typography variant="subtitle1">{name}</Typography>
          </MuiLink>
          <Typography variant="caption" color="text.secondary">Plays: {formatNumber(plays)}</Typography>
        </Box>
        <Box sx={{ mt: 1 }}>
          <Typography variant="body2">
            Best: <BestScore highestScore={highestScore} bestSubmissionId={bestSubmissionId} />
          </Typography>
          {lastPlayed ? <Typography variant="body2" color="text.secondary">Last played: {timeAgo(lastPlayed)}</Typography> : null}
        </Box>
        {scoreHistory && scoreHistory.length > 0 && (
          <Box sx={{ mt: 1 }}>
            <ScoreSparkline history={scoreHistory} rankingMode={rankingMode} />
          </Box>
        )}
      </CardContent>
    </Card>
  )
}

/** The best score, linked to its submission when known; a dash without one. */
function BestScore({ highestScore, bestSubmissionId }) {
  if (highestScore == null) return '—'
  if (bestSubmissionId == null) return formatNumber(highestScore)
  return (
    <MuiLink component={Link} to={`/submission/${bestSubmissionId}`} underline="hover">{formatNumber(highestScore)}</MuiLink>
  )
}
