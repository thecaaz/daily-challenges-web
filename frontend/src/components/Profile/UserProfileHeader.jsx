import React from 'react'
import { Box, Avatar, Typography, LinearProgress, Grid, Chip } from '@mui/material'
import timeAgo from '../../utils/timeAgo'

export default function UserProfileHeader({ profile }) {
  const username = profile.username || profile.Username || 'user'
  const level = profile.level ?? profile.Level ?? 1
  const xpInto = profile.xpIntoLevel ?? profile.XpIntoLevel ?? 0
  const xpToNext = profile.xpToNextLevel ?? profile.XpToNextLevel ?? 0
  const totalXp = profile.totalXp ?? profile.TotalXp ?? 0
  const streak = profile.streak ?? profile.Streak ?? 0
  const last = profile.lastSubmissionAt ?? profile.LastSubmissionAt

  const percent = xpToNext > 0 ? Math.round((xpInto / xpToNext) * 100) : 0

  return (
    <Box>
      <Grid container spacing={2} alignItems="center">
        <Grid item>
          <Avatar sx={{ width: 72, height: 72 }}>{username.charAt(0).toUpperCase()}</Avatar>
        </Grid>
        <Grid item xs>
          <Typography variant="h5">{username}</Typography>
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mt: 1 }}>
            <Chip label={`Level ${level}`} size="small" />
            <Typography variant="body2" color="text.secondary">{totalXp} XP</Typography>
            <Typography variant="body2" color="text.secondary">Streak: {streak}</Typography>
            {last ? <Typography variant="body2" color="text.secondary">Last: {timeAgo(last)}</Typography> : null}
          </Box>

          <Box sx={{ mt: 1 }}>
            <LinearProgress variant="determinate" value={percent} sx={{ height: 8, borderRadius: 1 }} />
            <Typography variant="caption" color="text.secondary">{xpInto}/{xpToNext} XP to next level ({percent}%)</Typography>
          </Box>
        </Grid>
      </Grid>
    </Box>
  )
}
