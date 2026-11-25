import React from 'react'
import { Box, Typography, Chip, LinearProgress, Tooltip } from '@mui/material'
import LocalFireDepartmentIcon from '@mui/icons-material/LocalFireDepartment'
import useXpGainFlash from '../../hooks/useXpGainFlash'
import formatNumber from '../../utils/formatNumber'

/** Level and XP numbers of `user`, with defaults for a user without XP data yet. */
export function getXpProgress(user) {
  const xpIntoLevel = user.xpIntoLevel ?? 0
  const xpForLevel = xpIntoLevel + (user.xpToNextLevel ?? 100)
  return {
    level: user.level ?? 1,
    streak: user.streak ?? 0,
    xpIntoLevel,
    xpForLevel,
    percent: xpForLevel > 0 ? Math.min(100, Math.round((xpIntoLevel / xpForLevel) * 100)) : 0,
  }
}

function StreakChip({ streak }) {
  return (
    <Chip
      icon={<LocalFireDepartmentIcon sx={{ fontSize: '0.9rem !important' }} />}
      label={streak}
      size="small"
      color="secondary"
      sx={{ height: 18, fontSize: '0.7rem', fontWeight: 700, ml: 0.5 }}
    />
  )
}

/** XP bar, level and streak of the logged-in user, flashing "+N XP" on gains. */
export default function XpStatus({ user }) {
  const { level, streak, xpIntoLevel, xpForLevel, percent } = getXpProgress(user)
  const gain = useXpGainFlash(user.totalXp ?? 0)
  const xpText = `${formatNumber(xpIntoLevel)}/${formatNumber(xpForLevel)}`
  const hasStreak = streak > 1

  return (
    <Box className="xp" sx={{ minWidth: { xs: 140, sm: 220 }, mr: 1 }}>
      <Box className="xp-bar-wrapper">
        <Tooltip title={`${formatNumber(xpIntoLevel)} / ${formatNumber(xpForLevel)} XP`} placement="bottom">
          <LinearProgress
            variant="determinate"
            value={percent}
            aria-label={`XP progress: ${formatNumber(xpIntoLevel)} of ${formatNumber(xpForLevel)}`}
          />
        </Tooltip>
        {gain && <span className="xp-gain" key={gain.id}>+{formatNumber(gain.amount)} XP</span>}
      </Box>
      <Box className="xp-meta" sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: '4px', flexWrap: 'wrap' }}>
        <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'none', sm: 'inline-flex' } }}>
          Level <Box component="strong" sx={{ ml: 0.5, fontWeight: 700 }}>{level}</Box> &nbsp;•&nbsp; {xpText} XP
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'inline-flex', sm: 'none' } }}>
          Lvl <Box component="strong" sx={{ ml: 0.5, fontWeight: 700 }}>{level}</Box>
        </Typography>
        {hasStreak && <StreakChip streak={streak} />}
      </Box>
    </Box>
  )
}
