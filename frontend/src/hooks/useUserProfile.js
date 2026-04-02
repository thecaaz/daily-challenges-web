import { useEffect, useState } from 'react'
import api from '../api'

export default function useUserProfile(userId, topGames = 10) {
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
    setLoading(true)
    setError(null)

    api.get(`/users/${userId}/profile`, { params: { topGames } })
      .then(r => r.data)
      .then(d => { if (!cancelled) setData(d) })
      .catch(e => { if (!cancelled) setError(e) })
      .finally(() => { if (!cancelled) setLoading(false) })

    return () => { cancelled = true }
  }, [userId, topGames])

  return { data, loading, error }
}
