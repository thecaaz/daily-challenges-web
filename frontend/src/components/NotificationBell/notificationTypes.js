const LEAGUE_TYPE_PREFIX = 'league_'
const FRIEND_TYPES = ['friend_request', 'friend_request_accepted']

export const DAY_WIN_TYPE = 'day_win'
export const ACHIEVEMENT_TYPE = 'achievement'

export const isFriendType = type => FRIEND_TYPES.includes(type)
export const isLeagueType = type => !!type?.startsWith(LEAGUE_TYPE_PREFIX)

/**
 * Where a notification leads, by its `type`, or null if it has nowhere to go.
 * Types: day_win, day_placement, friend_request, friend_request_accepted,
 * league_invite, league_joined, achievement.
 */
export function notificationRoute(n, user) {
  const type = n?.type ?? ''
  if (isFriendType(type)) return '/friends'
  if (isLeagueType(type)) return '/leagues'
  if (type === ACHIEVEMENT_TYPE) return user?.id != null ? `/users/${user.id}` : null
  if (n?.gameId) return gameRoute(n)
  return null
}

function gameRoute({ gameId, scoringDay }) {
  return scoringDay
    ? `/games/${gameId}?scoringDay=${encodeURIComponent(scoringDay)}`
    : `/games/${gameId}`
}
