import React from 'react'
import { Box, Grid, Skeleton, Stack } from '@mui/material'
import { statStripSx } from './DashboardStats'

/** Placeholder with the dashboard's layout while it loads. */
export default function DashboardSkeleton() {
  return (
    <Box>
      <Skeleton variant="text" width={200} height={36} sx={{ mb: 3 }} />
      <Stack direction="row" sx={statStripSx}>
        {[1, 2, 3].map(i => <Skeleton key={i} variant="rounded" width={150} height={64} />)}
      </Stack>
      <Grid container spacing={2}>
        {[1, 2].map(i => (
          <Grid item xs={12} sm={6} lg={4} key={i}>
            <Skeleton variant="rounded" height={300} />
          </Grid>
        ))}
        <Grid item xs={12}>
          <Skeleton variant="rounded" height={200} />
        </Grid>
      </Grid>
    </Box>
  )
}
