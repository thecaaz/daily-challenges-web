import React from 'react'
import { Typography } from '@mui/material'
import LockIcon from '@mui/icons-material/Lock'
import formatNumber from '../../utils/formatNumber'

const hiddenScoreSx = { fontWeight: 700, lineHeight: 1, display: 'flex', alignItems: 'center', gap: 0.5 }
const scoreSx = { fontWeight: 800, color: 'text.primary', lineHeight: 1 }

/** The formatted score, or a "Hidden" placeholder while it is masked. */
export default function ScoreValue({ submission, masked }) {
  if (masked) {
    return (
      <Typography variant="h6" color="text.secondary" sx={hiddenScoreSx}>
        <LockIcon fontSize="small" /> Hidden
      </Typography>
    )
  }
  return (
    <Typography variant="h6" sx={scoreSx}>
      {formatNumber(submission.score)}
    </Typography>
  )
}
