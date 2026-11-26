import React from 'react'
import { Box, Typography, Chip } from '@mui/material'
import GameListRow from './GameListRow'
import timeAgo from '../../utils/timeAgo'

const MAX_FRIEND_CHIPS = 4

const FRIEND_ACTIVITY_ROW_LOOK = { avatarColor: 'secondary.dark', titleMaxWidth: 140 }

export default function FriendActivityRow({ activity }) {
  const game = gameOfActivity(activity)
  const submissions = activity.recentSubmissions || []
  const latest = submissions[0]
  const shownSubmissions = uniqueBySubmitter(submissions).slice(0, MAX_FRIEND_CHIPS)

  return (
    <GameListRow game={game} look={FRIEND_ACTIVITY_ROW_LOOK}>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.4, mt: 0.25 }}>
        {shownSubmissions.map(submission => (
          <Chip
            key={submission.userId ?? submission.username}
            label={submission.username}
            size="small"
            sx={{ height: 18, fontSize: '0.65rem' }}
          />
        ))}
        {latest && (
          <Typography variant="caption" color="text.secondary" sx={{ alignSelf: 'center', ml: 0.5 }}>
            {timeAgo(latest.submittedAt)}
          </Typography>
        )}
      </Box>
    </GameListRow>
  )
}

function gameOfActivity(activity) {
  return {
    id: activity.gameId,
    url: activity.gameUrl,
    name: activity.gameName,
    imageUrl: activity.gameImageUrl,
    isFavorite: activity.isFavorite,
    hasSubmittedForLatest: activity.hasSubmittedForLatest,
  }
}

/** The first submission of each submitter (usernames compared case-insensitively). */
function uniqueBySubmitter(submissions) {
  const seenUsernames = new Set()
  return submissions.filter(submission => {
    const username = (submission.username || '').toLowerCase()
    if (seenUsernames.has(username)) return false
    seenUsernames.add(username)
    return true
  })
}
