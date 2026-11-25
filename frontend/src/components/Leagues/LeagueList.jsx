import React from 'react'
import { Typography, List, ListItem, ListItemText, Chip, Divider } from '@mui/material'
import { Link } from 'react-router-dom'

const leagueLinkSx = { borderRadius: 1, '&:hover': { bgcolor: 'action.hover' }, textDecoration: 'none', color: 'inherit' }

function leagueSummary(league) {
  const memberLabel = `${league.memberCount} member${league.memberCount !== 1 ? 's' : ''}`
  return `${memberLabel} · Owner: ${league.ownerUsername}`
}

/** The user's leagues, each linking to its detail page; owned ones get a chip. */
export default function LeagueList({ leagues, currentUserId }) {
  if (leagues.length === 0) {
    return (
      <Typography color="text.secondary">
        You are not in any leagues yet. Create one or ask a friend to invite you.
      </Typography>
    )
  }

  const isOwnedByCurrentUser = league => currentUserId != null && league.ownerId === currentUserId

  return (
    <List disablePadding>
      {leagues.map((league, index) => (
        <React.Fragment key={league.id}>
          {index > 0 && <Divider />}
          <ListItem component={Link} to={`/leagues/${league.id}`} sx={leagueLinkSx}>
            <ListItemText primary={league.name} secondary={leagueSummary(league)} />
            {isOwnedByCurrentUser(league) && <Chip label="Owner" size="small" color="primary" sx={{ ml: 1 }} />}
          </ListItem>
        </React.Fragment>
      ))}
    </List>
  )
}
