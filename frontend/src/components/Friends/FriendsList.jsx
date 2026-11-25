import React from 'react'
import { Typography, Button, Paper, Chip } from '@mui/material'
import formatNumber from '../../utils/formatNumber'
import FriendListItem, { DividedList } from './FriendListItem'

function friendStats(friend) {
  return `Level ${friend.level ?? 1} · ${formatNumber(friend.totalXp ?? 0)} XP · Streak: ${friend.streak ?? 0}`
}

export default function FriendsList({ friends, actions }) {
  const hasFriends = friends.length > 0

  return (
    <Paper>
      <Typography variant="h6" sx={{ p: 2, pb: 0 }}>
        Your Friends {hasFriends && <Chip label={friends.length} size="small" sx={{ ml: 1 }} />}
      </Typography>
      {hasFriends ? (
        <DividedList
          items={friends}
          getKey={friend => friend.userId}
          renderItem={friend => (
            <FriendListItem userId={friend.userId} username={friend.username} secondary={friendStats(friend)}>
              <Button size="small" variant="outlined" color="error" onClick={() => actions.removeFriend(friend)} disabled={actions.isFriendBusy(friend)}>
                Remove
              </Button>
            </FriendListItem>
          )}
        />
      ) : (
        <Typography color="text.secondary" sx={{ p: 2 }}>
          No friends yet. Send a request above!
        </Typography>
      )}
    </Paper>
  )
}
