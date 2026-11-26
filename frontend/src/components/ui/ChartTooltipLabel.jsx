import React from 'react'
import { Typography } from '@mui/material'
import { chartTooltip } from '../../styles/tokens'

const labelSx = { background: chartTooltip.background, color: chartTooltip.text, px: 0.75, py: 0.25, borderRadius: 1 }

/** Dark hover label for the content of a small Recharts chart's tooltip. */
export default function ChartTooltipLabel({ children }) {
  return <Typography variant="caption" sx={labelSx}>{children}</Typography>
}
