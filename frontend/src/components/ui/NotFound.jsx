import React from 'react'
import { Typography } from '@mui/material'

export default function NotFound({ message = 'Not found' }) {
  return (
    <Typography variant="h5" component="h1" role="alert">
      {message}
    </Typography>
  )
}
