import React from 'react'
import {
  Box, Typography, Grid, Paper, Chip, Divider,
  Avatar, ListItem, ListItemAvatar, ListItemText,
} from '@mui/material'
import { Link } from 'react-router-dom'
import PeopleIcon from '@mui/icons-material/People'
import WhatshotIcon from '@mui/icons-material/Whatshot'
import timeAgo from '../../utils/timeAgo'

function FriendItem({ friend }) {
  const name = friend.username || '?'
  return (
    <ListItem
      component={Link}
      to={`/users/${friend.userId}`}
      disableGutters
      sx={{
        px: 2,
        py: 1.25,
        gap: 1.5,
        textDecoration: 'none',
        color: 'inherit',
        transition: 'background 0.15s',
        '&:hover': { bgcolor: 'action.hover' },
      }}
    >
      <ListItemAvatar sx={{ minWidth: 44 }}>
        <Avatar sx={{ width: 38, height: 38, bgcolor: 'secondary.main', fontWeight: 700, fontSize: '0.9rem' }}>
          {name.slice(0, 2).toUpperCase()}
        </Avatar>
      </ListItemAvatar>
      <ListItemText
        primary={<Typography variant="body2" fontWeight={600}>{name}</Typography>}
        secondary={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap', mt: 0.25 }}>
            <Chip label={`Lv ${friend.level}`} size="small" color="primary" sx={{ height: 18, fontSize: '0.65rem' }} />
            {friend.streak > 0 && (
              <Chip
                icon={<WhatshotIcon sx={{ fontSize: '12px !important' }} />}
                label={friend.streak}
                size="small"
                color="warning"
                sx={{ height: 18, fontSize: '0.65rem' }}
              />
            )}
            {friend.lastSubmissionAt
              ? <Typography variant="caption" color="text.secondary">{timeAgo(friend.lastSubmissionAt)}</Typography>
              : <Typography variant="caption" color="text.disabled">never active</Typography>}
          </Box>
        }
        disableTypography
      />
    </ListItem>
  )
}

/** Full-width friends list shown at the bottom of the dashboard. */
export default function FriendsPanel({ friends = [] }) {
  return (
    <Paper
      elevation={0}
      sx={{ borderRadius: 3, border: '1px solid', borderColor: 'divider', overflow: 'hidden' }}
    >
      <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1, bgcolor: 'background.default' }}>
        <PeopleIcon fontSize="small" color="secondary" />
        <Typography variant="subtitle1" fontWeight={700}>Friends</Typography>
        <Chip label={friends.length} size="small" sx={{ ml: 0.5 }} />
      </Box>
      <Divider />
      {friends.length === 0 ? (
        <Box sx={{ px: 2, py: 3, textAlign: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            No friends yet.{' '}
            <Link to="/friends" style={{ color: 'inherit' }}>Add some!</Link>
          </Typography>
        </Box>
      ) : (
        <Grid container spacing={0}>
          {friends.map((friend, i) => (
            <Grid item xs={12} sm={6} md={4} lg={3} key={friend.userId ?? i}>
              {i > 0 && <Divider />}
              <FriendItem friend={friend} />
            </Grid>
          ))}
        </Grid>
      )}
    </Paper>
  )
}
