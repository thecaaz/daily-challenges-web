import React from 'react'
import { Box, Typography } from '@mui/material'
import GroupsIcon from '@mui/icons-material/Groups'

/** League name and summary line; `children` are the action buttons. */
export default function LeagueHeader({ league, children }) {
  const memberCount = league.memberCount ?? league.members?.length

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <GroupsIcon /> {league.name}
        </Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {children}
        </Box>
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {memberCount} members · Owner: {league.ownerUsername}
      </Typography>
    </>
  )
}
