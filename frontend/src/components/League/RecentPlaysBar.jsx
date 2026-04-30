import React from 'react'
import { ResponsiveContainer, BarChart, Bar, Tooltip } from 'recharts'
import { Typography } from '@mui/material'
import { useTheme } from '@mui/material/styles'
import dayjs from 'dayjs'

function PlaysTooltip({ active, payload }) {
  if (!active || !payload || payload.length === 0) return null
  const { label, count } = payload[0].payload
  return (
    <Typography
      variant="caption"
      sx={{ background: 'rgba(0,0,0,0.75)', color: '#fff', px: 0.75, py: 0.25, borderRadius: 1 }}
    >
      {label}: {count} play{count !== 1 ? 's' : ''}
    </Typography>
  )
}

export default function RecentPlaysBar({ recentPlays, days = 7 }) {
  if (!recentPlays || recentPlays.length === 0) return null

  const theme = useTheme()
  const today = dayjs()

  const data = recentPlays.map((count, i) => {
    const daysAgo = recentPlays.length - 1 - i
    const label = daysAgo === 0 ? 'Today' : today.subtract(daysAgo, 'day').format('MMM D')
    return { label, count }
  })

  return (
    <ResponsiveContainer width="100%" height={40}>
      <BarChart data={data} margin={{ top: 2, right: 0, bottom: 0, left: 0 }} barCategoryGap="20%">
        <Tooltip content={<PlaysTooltip />} cursor={{ fill: 'rgba(255,255,255,0.05)' }} />
        <Bar
          dataKey="count"
          fill={theme.palette.primary.main}
          radius={[2, 2, 0, 0]}
          isAnimationActive={false}
        />
      </BarChart>
    </ResponsiveContainer>
  )
}
