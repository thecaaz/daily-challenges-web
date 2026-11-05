import { useEffect, useState, useCallback, useRef } from 'react'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

/**
 * Friendship status with `targetUserId`: 'none' | 'pending_sent' | 'pending_received' | 'friends'.
 * A failed lookup sets `error` (and leaves `status` as it was) instead of
 * pretending the users aren't friends.
 */
export default function useFriendStatus(targetUserId) {
  const auth = useAuth()
  const userId = auth?.user?.id ?? null
  const [status, setStatus] = useState('none')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(false)
  const requestRef = useRef(0)

  const fetchStatus = useCallback(() => {
    const requestId = ++requestRef.current
    if (!targetUserId || userId == null) {
      setStatus('none')
      setError(false)
      setLoading(false)
      return
    }

    setLoading(true)
    setError(false)
    api.get(`/friends/status/${targetUserId}`)
      .then(r => { if (requestId === requestRef.current) setStatus(r?.data?.status ?? 'none') })
      .catch(() => { if (requestId === requestRef.current) setError(true) })
      .finally(() => { if (requestId === requestRef.current) setLoading(false) })
  }, [targetUserId, userId])

  useEffect(() => {
    fetchStatus()
  }, [fetchStatus])

  return { status, loading, error, refresh: fetchStatus }
}
