import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import useAsyncData from './useAsyncData'

/**
 * Loads a user's profile. `error` is the axios error (check `error.response.status`
 * for 404). Re-fetches when the viewer changes, because the owner (and admins)
 * also see the current day's entries (otherwise `scoresHidden` is true).
 * Without a `userId` nothing is loaded and `loading` is false.
 */
export default function useUserProfile(userId, topGames = 10) {
  const auth = useAuth()
  const viewerId = auth?.user?.id ?? null
  const hasUser = userId != null

  const { data, loading, failure } = useAsyncData(
    () => (hasUser ? getProfile(userId, topGames) : null),
    [userId, topGames, viewerId],
  )

  return { data, loading: hasUser && loading, error: failure }
}

function getProfile(userId, topGames) {
  return api.get(`/users/${userId}/profile`, { params: { topGames } }).then(response => response.data)
}
