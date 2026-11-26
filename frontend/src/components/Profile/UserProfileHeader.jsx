import React from 'react'
import { Box, Avatar, Typography, LinearProgress, Grid, Chip } from '@mui/material'
import timeAgo from '../../utils/timeAgo'
import formatNumber from '../../utils/formatNumber'
import FriendButton from './FriendButton'
import UserLink from '../ui/UserLink'

export default function UserProfileHeader({ profile }) {
  const username = profile.username || 'user'
  const userId = profile.userId
  const level = profile.level ?? 1
  const xpIntoLevel = profile.xpIntoLevel ?? 0
  const xpToNextLevel = profile.xpToNextLevel ?? 0
  const totalXp = profile.totalXp ?? 0
  const streak = profile.streak ?? 0
  const lastSubmissionAt = profile.lastSubmissionAt

  const totalForLevel = xpIntoLevel + xpToNextLevel
  const percent = totalForLevel > 0 ? Math.round((xpIntoLevel / totalForLevel) * 100) : 0

  return (
    <Box>
      <Grid container spacing={2} alignItems="center">
        <Grid item>
          <Avatar sx={{ width: 72, height: 72 }}>{username.charAt(0).toUpperCase()}</Avatar>
        </Grid>
        <Grid item xs>
          {userId ? (
            <UserLink userId={userId}>
              <Typography variant="h5">{username}</Typography>
            </UserLink>
          ) : (
            <Typography variant="h5">{username}</Typography>
          )}
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mt: 1 }}>
            <Chip label={`Level ${level}`} size="small" />
            <Typography variant="body2" color="text.secondary">{formatNumber(totalXp)} XP</Typography>
            <Typography variant="body2" color="text.secondary">Streak: {streak}</Typography>
            {lastSubmissionAt ? <Typography variant="body2" color="text.secondary">Last: {timeAgo(lastSubmissionAt)}</Typography> : null}
          </Box>

          <Box sx={{ mt: 1 }}>
            <LinearProgress variant="determinate" value={percent} sx={{ height: 8, borderRadius: 1 }} />
            <Typography variant="caption" color="text.secondary">{formatNumber(xpIntoLevel)}/{formatNumber(totalForLevel)} XP ({formatNumber(xpToNextLevel)} to go) — {percent}%</Typography>
          </Box>
          {userId && <Box sx={{ mt: 1.5 }}><FriendButton targetUserId={userId} /></Box>}
        </Grid>
      </Grid>
    </Box>
  )
}
