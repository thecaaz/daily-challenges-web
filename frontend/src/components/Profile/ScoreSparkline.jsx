import React from 'react'
import { ResponsiveContainer, AreaChart, Area, Tooltip } from 'recharts'
import { Typography } from '@mui/material'
import { useTheme } from '@mui/material/styles'

function SparklineTooltip({ active, payload }) {
  if (!active || !payload || payload.length === 0) return null
  const { scoringDay, scoreValue } = payload[0].payload
  if (scoreValue == null) return null
  return (
    <Typography
      variant="caption"
      sx={{ background: 'rgba(0,0,0,0.75)', color: '#fff', px: 0.75, py: 0.25, borderRadius: 1 }}
    >
      {scoringDay}: {scoreValue}
    </Typography>
  )
}

export default function ScoreSparkline({ history, rankingMode }) {
  if (!history || history.length === 0) return null

  const hasValues = history.some(h => (h.scoreValue ?? h.ScoreValue) != null)
  if (!hasValues) return null

  const theme = useTheme()
  const color = theme.palette.primary.main

  const data = history.map(h => ({
    scoringDay: h.scoringDay ?? h.ScoringDay,
    scoreValue: h.scoreValue ?? h.ScoreValue,
  }))

  return (
    <>
      <ResponsiveContainer width="100%" height={54}>
        <AreaChart data={data} margin={{ top: 4, right: 0, bottom: 0, left: 0 }}>
          <defs>
            <linearGradient id="sparkGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor={color} stopOpacity={0.3} />
              <stop offset="95%" stopColor={color} stopOpacity={0} />
            </linearGradient>
          </defs>
          <Tooltip content={<SparklineTooltip />} />
          <Area
            type="monotone"
            dataKey="scoreValue"
            stroke={color}
            strokeWidth={1.5}
            fill="url(#sparkGrad)"
            dot={false}
            activeDot={{ r: 3 }}
            connectNulls={false}
            isAnimationActive={false}
          />
        </AreaChart>
      </ResponsiveContainer>
      {rankingMode === 'lowest' && (
        <Typography variant="caption" color="text.disabled" sx={{ display: 'block', textAlign: 'right' }}>
          lower is better
        </Typography>
      )}
    </>
  )
}
