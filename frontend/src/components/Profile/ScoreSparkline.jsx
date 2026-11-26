import React, { useId } from 'react'
import { ResponsiveContainer, AreaChart, Area, Tooltip } from 'recharts'
import { Typography } from '@mui/material'
import { useTheme } from '@mui/material/styles'
import ChartTooltipLabel from '../ui/ChartTooltipLabel'

function SparklineTooltip({ active, payload }) {
  if (!active || !payload || payload.length === 0) return null
  const { scoringDay, scoreValue } = payload[0].payload
  if (scoreValue == null) return null
  return <ChartTooltipLabel>{scoringDay}: {scoreValue}</ChartTooltipLabel>
}

export default function ScoreSparkline({ history, rankingMode }) {
  const theme = useTheme()
  const instanceId = useId()
  const gradientId = `sparkGrad-${instanceId.replace(/:/g, '')}`
  if (!history || history.length === 0) return null

  const hasValues = history.some(entry => entry.scoreValue != null)
  if (!hasValues) return null

  const color = theme.palette.primary.main

  const data = history.map(({ scoringDay, scoreValue }) => ({ scoringDay, scoreValue }))
  const valueCount = data.filter(point => point.scoreValue != null).length
  const isSinglePoint = valueCount === 1
  const singlePointAsDot = isSinglePoint ? { r: 3, fill: color, stroke: color } : false

  return (
    <>
      <ResponsiveContainer width="100%" height={54}>
        <AreaChart data={data} margin={{ top: 4, right: 0, bottom: 0, left: 0 }}>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
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
            fill={`url(#${gradientId})`}
            dot={singlePointAsDot}
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
