import React from 'react'
import { Grid } from '@mui/material'
import SubmissionCard from '../../SubmissionCard'

const gridSx = { mt: 1 }

/** Responsive grid of submission cards; `cardPropsFor(submission)` adds props to each card. */
export default function SubmissionGrid({ submissions, cardPropsFor }) {
  if (!submissions || submissions.length === 0) return null

  return (
    <Grid container spacing={2} sx={gridSx}>
      {submissions.map(submission => (
        <Grid item xs={12} sm={6} md={4} key={submission.id}>
          <SubmissionCard submission={submission} {...cardPropsFor?.(submission)} />
        </Grid>
      ))}
    </Grid>
  )
}
