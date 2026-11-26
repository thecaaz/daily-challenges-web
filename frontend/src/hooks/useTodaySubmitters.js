import api from '../api'
import useAsyncData from './useAsyncData'

const NO_SUBMITTERS = Object.freeze({ count: 0, usernames: Object.freeze([]) })

/**
 * Who already submitted today's score for a game: `{ count, usernames }`, or
 * `null` while loading. A failed lookup counts as nobody, so the hidden-scores
 * card invites the player to be first instead of showing an error.
 */
export default function useTodaySubmitters(gameId) {
  const { data, error } = useAsyncData(
    () => api.get(`/submissions/game/${gameId}/today-submitters`).then(response => response.data),
    [gameId]
  )
  return error ? NO_SUBMITTERS : data
}
