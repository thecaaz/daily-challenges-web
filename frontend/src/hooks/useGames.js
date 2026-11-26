import api from '../api'
import useAsyncData from './useAsyncData'

const toGameList = data => (Array.isArray(data) ? data : [])

/**
 * All games (an empty list while loading or when the response is not a list).
 *
 * @returns {{ games: object[], loading: boolean, error: boolean, retry: () => void }}
 */
export default function useGames() {
  const { data, loading, error, refetch } = useAsyncData(
    () => api.get('/games').then(response => toGameList(response.data)),
    []
  )
  return { games: data ?? [], loading, error, retry: refetch }
}
