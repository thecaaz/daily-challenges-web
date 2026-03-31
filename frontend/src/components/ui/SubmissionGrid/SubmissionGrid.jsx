import React from 'react'
import { Grid } from '@mui/material'

export default function SubmissionGrid({ items = [], ItemComponent, itemKey = 'id', xs = 12, sm = 6, md = 4, containerSx, itemProps }) {
  if (!items || items.length === 0) return null

  return (
    <Grid container spacing={2} sx={containerSx}>
      {items.map(i => (
        <Grid item xs={xs} sm={sm} md={md} key={i[itemKey]}>
          <ItemComponent submission={i} {...(typeof itemProps === 'function' ? itemProps(i) : (itemProps || {}))} />
        </Grid>
      ))}
    </Grid>
  )
}
