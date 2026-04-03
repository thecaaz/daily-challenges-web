import React from 'react'
import { Card, CardContent, Typography, Box, Link as MuiLink } from '@mui/material'
import { Link } from 'react-router-dom'
import timeAgo from '../../utils/timeAgo'
import formatNumber from '../../utils/formatNumber'

export default function UserGameStatCard({ game }) {
  const id = game.gameId ?? game.GameId
  const name = game.name ?? game.Name ?? 'Game'
  const plays = game.plays ?? game.Plays ?? 0
  const best = game.highestScore ?? game.HighestScore
  const bestId = game.bestSubmissionId ?? game.BestSubmissionId
  const last = game.lastPlayed ?? game.LastPlayed

  return (
    <Card variant="outlined">
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
          <MuiLink component={Link} to={`/games/${id}`} underline="hover">
            <Typography variant="subtitle1">{name}</Typography>
          </MuiLink>
          <Typography variant="caption" color="text.secondary">Plays: {formatNumber(plays)}</Typography>
        </Box>
        <Box sx={{ mt: 1 }}>
          <Typography variant="body2">
            Best: {best != null ? (
              bestId != null ? (
                <MuiLink component={Link} to={`/submission/${bestId}`} underline="hover">{formatNumber(best)}</MuiLink>
              ) : (
                formatNumber(best)
              )
            ) : '—'}
          </Typography>
          {last ? <Typography variant="body2" color="text.secondary">Last played: {timeAgo(last)}</Typography> : null}
        </Box>
      </CardContent>
    </Card>
  )
}
