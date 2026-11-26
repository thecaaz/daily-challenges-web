import api from '../api'
import useAsyncData from './useAsyncData'

const NO_ACHIEVEMENTS = Object.freeze([])

/**
 * A user's achievements, unlocked or not. Without a `userId` nothing is
 * loaded. A failed lookup yields no achievements, so the profile shows no
 * achievements section rather than an error.
 *
 * @returns {{ achievements: object[]|null, loading: boolean }}
 */
export default function useAchievements(userId) {
  const { data, loading, error } = useAsyncData(
    () => (userId ? api.get(`/users/${userId}/achievements`).then(response => response.data) : null),
    [userId]
  )
  if (!userId) return { achievements: null, loading: false }
  return { achievements: error ? NO_ACHIEVEMENTS : data, loading }
}
