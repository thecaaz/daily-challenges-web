import { useEffect, useState } from 'react'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

/**
 * Loads a user's profile. `error` is the axios error (check `error.response.status`
 * for 404). Re-fetches when the viewer changes, because the owner (and admins)
 * also see the current day's entries (otherwise `scoresHidden` is true).
 */
export default function useUserProfile(userId, topGames = 10) {
  const auth = useAuth()
  const viewerId = auth?.user?.id ?? null
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(Boolean(userId))
  const [error, setError] = useState(null)

  useEffect(() => {
    if (userId == null) {
      setData(null)
      setLoading(false)
      setError(null)
      return
    }

    let cancelled = false
    setData(null)
    setLoading(true)
    setError(null)

    api.get(`/users/${userId}/profile`, { params: { topGames } })
      .then(r => r.data)
      .then(d => { if (!cancelled) setData(d) })
      .catch(e => { if (!cancelled) setError(e) })
      .finally(() => { if (!cancelled) setLoading(false) })

    return () => { cancelled = true }
  }, [userId, topGames, viewerId])

  return { data, loading, error }
}
