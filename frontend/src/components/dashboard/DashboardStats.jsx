import React from 'react'
import { Box, Stack } from '@mui/material'
import PeopleIcon from '@mui/icons-material/People'
import TodayIcon from '@mui/icons-material/Today'
import SportsSoccerIcon from '@mui/icons-material/SportsSoccer'
import WhatshotIcon from '@mui/icons-material/Whatshot'
import BoltIcon from '@mui/icons-material/Bolt'
import AppButton from '../ui/AppButton'
import StatCard from './StatCard'

export const statStripSx = { mb: 3, flexWrap: 'wrap', gap: 2, '& > *': { flex: '1 1 140px', maxWidth: { md: 280 } } }

const pluralize = (count, noun) => `${count} ${noun}${count !== 1 ? 's' : ''}`

/** Row of today's stat cards plus the button that starts the favorites player. */
export default function DashboardStats({ summary, favorites }) {
  const { activeGameCount, submissionCountToday, friends, friendActivity, xpEarnedToday } = summary
  const hasFriendActivity = friendActivity.length > 0
  const hasEarnedXpToday = xpEarnedToday > 0
  const playLabel = favorites.checkingPlayable ? '…' : favorites.playableCount

  return (
    <Stack direction="row" sx={statStripSx}>
      <StatCard icon={<TodayIcon fontSize="small" />} label="Active today" value={activeGameCount} color="primary" />
      <StatCard icon={<SportsSoccerIcon fontSize="small" />} label="Submissions today" value={submissionCountToday} color="success" />
      <StatCard icon={<PeopleIcon fontSize="small" />} label="Friends" value={friends.length} color="secondary" />
      {hasFriendActivity && (
        <StatCard
          icon={<WhatshotIcon fontSize="small" />}
          label="Friend activity"
          value={pluralize(friendActivity.length, 'game')}
          color="warning"
        />
      )}
      {hasEarnedXpToday && (
        <StatCard icon={<BoltIcon fontSize="small" />} label="XP earned today" value={`+${xpEarnedToday}`} color="warning" />
      )}

      <Box sx={{ alignContent: 'end', mb: 3, display: 'flex', alignItems: 'flex-start' }}>
        <Box sx={{ flex: 1 }} />
        <AppButton to="/dashboard/play" variant="contained" disabled={favorites.playableCount === 0} sx={{ mt: 0.5 }}>
          Play ({playLabel})
        </AppButton>
      </Box>
    </Stack>
  )
}
