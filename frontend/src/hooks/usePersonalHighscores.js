import api from '../api'
import useAsyncData from './useAsyncData'

/**
 * The current user's best submissions for a game, loaded once auth has
 * finished, a user is logged in and the game itself is known.
 *
 * @returns {{ highscores: object[], error: boolean, retry: () => void }}
 */
export default function usePersonalHighscores(gameId, { userId, authLoading, hasGame }) {
  const canLoad = !authLoading && userId && hasGame
  const { data, error, refetch } = useAsyncData(
    () => canLoad
      ? api.get(`/games/${gameId}/personal-highscore`).then(r => (r.data || {}).top || [])
      : Promise.resolve(null),
    [authLoading, userId, hasGame, gameId]
  )

  return { highscores: data ?? [], error, retry: refetch }
}
