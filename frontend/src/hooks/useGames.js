import { getGames } from '../api/games'
import useAsyncData from './useAsyncData'

/**
 * All games (an empty list while loading or when the response is not a list).
 *
 * @returns {{ games: object[], loading: boolean, error: boolean, retry: () => void }}
 */
export default function useGames() {
  const { data, loading, error, refetch } = useAsyncData(getGames, [])
  return { games: data ?? [], loading, error, retry: refetch }
}
