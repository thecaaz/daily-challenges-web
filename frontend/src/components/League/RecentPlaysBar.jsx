import React from 'react'
import { ResponsiveContainer, BarChart, Bar, Tooltip } from 'recharts'
import { useTheme } from '@mui/material/styles'
import ChartTooltipLabel from '../ui/ChartTooltipLabel'
import { chartTooltip } from '../../styles/tokens'

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const CURSOR = { fill: chartTooltip.cursorFill }
const ISO_DAY = /^(\d{4})-(\d{2})-(\d{2})/

/** "yyyy-MM-dd" -> "MMM D", without going through a (timezone-dependent) Date. */
function formatIsoDay(iso) {
  const match = ISO_DAY.exec(iso || '')
  if (!match) return iso || ''
  const [, , month, day] = match
  return `${MONTHS[Number(month) - 1] ?? month} ${Number(day)}`
}

/**
 * Bar data for the API's `recentPlays` counts and the scoring days they count
 * (`recentPlayDays`, index for index, oldest -> newest); the newest is "Today".
 */
export function buildRecentPlaysData(recentPlays, recentPlayDays) {
  if (!recentPlays || recentPlays.length === 0) return []
  const last = recentPlays.length - 1
  return recentPlays.map((count, i) => {
    const isoDate = String(recentPlayDays[i]).slice(0, 10)
    const label = i === last ? 'Today' : formatIsoDay(isoDate)
    return { label, count, isoDate }
  })
}

function PlaysTooltip({ active, payload }) {
  if (!active || !payload || payload.length === 0) return null
  const { label, count } = payload[0].payload
  const plays = count === 1 ? 'play' : 'plays'
  return <ChartTooltipLabel>{label}: {count} {plays}</ChartTooltipLabel>
}

export default function RecentPlaysBar({ recentPlays, recentPlayDays, onDayClick }) {
  const theme = useTheme()
  if (!recentPlays || recentPlays.length === 0) return null

  const data = buildRecentPlaysData(recentPlays, recentPlayDays)

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
        <Tooltip content={<PlaysTooltip />} cursor={CURSOR} />
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
