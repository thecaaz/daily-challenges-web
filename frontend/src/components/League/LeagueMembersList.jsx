import React from 'react'
import {
  Box, List, ListItem, ListItemText, ListItemAvatar, Avatar,
  IconButton, Chip, Divider, Tooltip,
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import UserLink from '../ui/UserLink'

/**
 * League members. When `canRemoveMembers`, every member other than the
 * current user gets a remove button.
 */
export default function LeagueMembersList({ league, currentUserId, canRemoveMembers, onRemove }) {
  const members = league.members || []

  return (
    <List disablePadding>
      {members.map((member, index) => (
        <React.Fragment key={member.userId}>
          {index > 0 && <Divider />}
          <MemberRow
            member={member}
            isLeagueOwner={member.role === 'owner' || member.userId === league.ownerId}
            canRemove={canRemoveMembers && member.userId !== currentUserId}
            onRemove={onRemove}
          />
        </React.Fragment>
      ))}
    </List>
  )
}

function MemberRow({ member, isLeagueOwner, canRemove, onRemove }) {
  const initial = member.username?.[0]?.toUpperCase()

  return (
    <ListItem disablePadding sx={{ py: 0.5 }}>
      <ListItemAvatar>
        <Avatar sx={{ width: 32, height: 32, fontSize: 14 }}>
          {initial}
        </Avatar>
      </ListItemAvatar>
      <ListItemText
        primary={
          <Box component="span" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <UserLink userId={member.userId}>{member.username}</UserLink>
            {isLeagueOwner && <Chip label="Owner" size="small" color="primary" />}
          </Box>
        }
        secondary={`Level ${member.level} · Streak ${member.streak}`}
      />
      {canRemove && (
        <Tooltip title="Remove member">
          <IconButton size="small" onClick={() => onRemove(member.userId, member.username)}>
            <DeleteOutlineIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      )}
    </ListItem>
  )
}
