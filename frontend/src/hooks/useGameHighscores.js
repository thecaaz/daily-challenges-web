import api from '../api'
import useAsyncData from './useAsyncData'

/**
 * Top submissions of a game, loaded once the game itself is known.
 *
 * Reloads when `hasSubmittedForLatest` changes, because submitting reveals
 * today's otherwise hidden entries.
 *
 * @returns {{ highscores: object[], error: boolean, retry: () => void }}
 */
export default function useGameHighscores(gameId, { hasGame, hasSubmittedForLatest }) {
  const { data, error, refetch } = useAsyncData(
    () => hasGame
      ? api.get(`/games/${gameId}/highscore`).then(r => (r.data || {}).top || [])
      : Promise.resolve(null),
    [hasGame, gameId, hasSubmittedForLatest]
  )

  return { highscores: data ?? [], error, retry: refetch }
}
