import React, { useEffect, useState } from 'react'
import { Box, Typography, CircularProgress } from '@mui/material'
import api from '../../api'
import AchievementBadge from './AchievementBadge'

export default function AchievementsGrid({ userId }) {
  const [achievements, setAchievements] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!userId) return
    let mounted = true

    api.get(`/users/${userId}/achievements`)
      .then(res => { if (mounted) setAchievements(res.data) })
      .catch(() => { if (mounted) setAchievements([]) })
      .finally(() => { if (mounted) setLoading(false) })

    return () => { mounted = false }
  }, [userId])

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}><CircularProgress size={24} /></Box>
  if (!achievements || achievements.length === 0) return null

  const unlocked = achievements.filter(a => a.unlockedAt || a.UnlockedAt)
  const locked = achievements.filter(a => !a.unlockedAt && !a.UnlockedAt)

  return (
    <Box>
      <Typography variant="h6" gutterBottom>
        Achievements
        {unlocked.length > 0 && (
          <Typography component="span" variant="body2" color="text.secondary" sx={{ ml: 1 }}>
            {unlocked.length}/{achievements.length}
          </Typography>
        )}
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
        {unlocked.map(a => (
          <AchievementBadge key={a.achievementId || a.AchievementId} achievement={a} />
        ))}
        {locked.map(a => (
          <AchievementBadge key={a.achievementId || a.AchievementId} achievement={a} />
        ))}
      </Box>
    </Box>
  )
}
