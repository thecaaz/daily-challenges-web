import React from 'react'
import { Box, List, ListItem, ListItemText, ListItemSecondaryAction, Divider } from '@mui/material'
import { Link } from 'react-router-dom'

const nameLinkSx = { textDecoration: 'none', color: 'inherit', fontWeight: 500 }

/** Dense list with a divider between consecutive items. */
export function DividedList({ items, getKey, renderItem }) {
  return (
    <List dense>
      {items.map((item, index) => (
        <React.Fragment key={getKey(item)}>
          {index > 0 && <Divider component="li" />}
          {renderItem(item)}
        </React.Fragment>
      ))}
    </List>
  )
}

/** A user's name linking to their profile, with row actions on the right. */
export default function FriendListItem({ userId, username, secondary, children }) {
  return (
    <ListItem>
      <ListItemText
        primary={<Box component={Link} to={`/users/${userId}`} sx={nameLinkSx}>{username}</Box>}
        secondary={secondary}
      />
      <ListItemSecondaryAction>{children}</ListItemSecondaryAction>
    </ListItem>
  )
}
