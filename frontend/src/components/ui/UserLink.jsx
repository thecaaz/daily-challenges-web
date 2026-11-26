import React from 'react'
import { Box } from '@mui/material'
import { Link } from 'react-router-dom'

const plainLinkSx = { textDecoration: 'none', color: 'inherit' }

/** Links to a user's profile, styled like the surrounding text. */
export default function UserLink({ userId, children }) {
  return <Box component={Link} to={`/users/${userId}`} sx={plainLinkSx}>{children}</Box>
}
