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

export default function RecentPlaysBar({ recentPlays, days = 7, onDayClick }) {
  if (!recentPlays || recentPlays.length === 0) return null

  const theme = useTheme()
  const today = dayjs()

  const data = recentPlays.map((count, i) => {
    const daysAgo = recentPlays.length - 1 - i
    const date = today.subtract(daysAgo, 'day')
    const label = daysAgo === 0 ? 'Today' : date.format('MMM D')
    const isoDate = date.format('YYYY-MM-DD')
    return { label, count, isoDate }
  })

  const handleClick = onDayClick
    ? (barData) => { if (barData?.isoDate) onDayClick(barData.isoDate) }
    : undefined

  return (
    <ResponsiveContainer width="100%" height={40}>
      <BarChart
        data={data}
        margin={{ top: 2, right: 0, bottom: 0, left: 0 }}
        barCategoryGap="20%"
      >
        <Tooltip content={<PlaysTooltip />} cursor={{ fill: 'rgba(255,255,255,0.05)' }} />
        <Bar
          dataKey="count"
          fill={theme.palette.primary.main}
          radius={[2, 2, 0, 0]}
          isAnimationActive={false}
          onClick={handleClick}
          style={onDayClick ? { cursor: 'pointer' } : undefined}
        />
      </BarChart>
    </ResponsiveContainer>
  )
}
