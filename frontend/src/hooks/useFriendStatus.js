import { useEffect, useState, useCallback } from 'react'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import useLatestRequest from './useLatestRequest'

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
  const startRequest = useLatestRequest()

  const fetchStatus = useCallback(() => {
    const isLatest = startRequest()
    if (!targetUserId || userId == null) {
      setStatus('none')
      setError(false)
      setLoading(false)
      return
    }

    setLoading(true)
    setError(false)
    api.get(`/friends/status/${targetUserId}`)
      .then(response => { if (isLatest()) setStatus(response?.data?.status ?? 'none') })
      .catch(() => { if (isLatest()) setError(true) })
      .finally(() => { if (isLatest()) setLoading(false) })
  }, [targetUserId, userId, startRequest])

  useEffect(() => {
    fetchStatus()
  }, [fetchStatus])

  return { status, loading, error, refresh: fetchStatus }
}
