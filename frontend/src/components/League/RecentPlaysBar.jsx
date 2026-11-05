import React from 'react'
import { ResponsiveContainer, BarChart, Bar, Tooltip } from 'recharts'
import { Typography } from '@mui/material'
import { useTheme } from '@mui/material/styles'

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const ISO_DAY = /^(\d{4})-(\d{2})-(\d{2})/

/** "yyyy-MM-dd" -> "MMM D", without going through a (timezone-dependent) Date. */
function formatIsoDay(iso) {
  const m = ISO_DAY.exec(iso || '')
  if (!m) return iso || ''
  return `${MONTHS[Number(m[2]) - 1] ?? m[2]} ${Number(m[3])}`
}

/** UTC calendar day `daysAgo` days before `now`, as "yyyy-MM-dd". */
function utcIsoDay(now, daysAgo) {
  const d = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - daysAgo))
  return d.toISOString().slice(0, 10)
}

/**
 * Build the bar data. Prefers the scoring days returned by the API
 * (`recentPlayDays`, parallel to `recentPlays`, oldest -> newest). Without
 * them, falls back to counting back from today's UTC date, which is what the
 * backend used before it returned the days explicitly.
 */
export function buildRecentPlaysData(recentPlays, recentPlayDays, now = new Date()) {
  if (!recentPlays || recentPlays.length === 0) return []
  const useApiDays = Array.isArray(recentPlayDays) && recentPlayDays.length === recentPlays.length
  const last = recentPlays.length - 1
  return recentPlays.map((count, i) => {
    const isoDate = useApiDays ? String(recentPlayDays[i]).slice(0, 10) : utcIsoDay(now, last - i)
    const label = i === last ? 'Today' : formatIsoDay(isoDate)
    return { label, count, isoDate }
  })
}

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
