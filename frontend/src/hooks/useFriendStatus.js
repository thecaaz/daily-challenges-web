import { useEffect, useState, useCallback } from 'react'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function useFriendStatus(targetUserId) {
  const { user } = useAuth()
  const [status, setStatus] = useState('none')
  const [loading, setLoading] = useState(false)

  const fetchStatus = useCallback(() => {
    if (!targetUserId || !user) {
      setStatus('none')
      setLoading(false)
      return
    }

    setLoading(true)
    api.get(`/friends/status/${targetUserId}`)
      .then(r => setStatus(r.data.status ?? 'none'))
      .catch(() => setStatus('none'))
      .finally(() => setLoading(false))
  }, [targetUserId, user])

  useEffect(() => {
    fetchStatus()
  }, [fetchStatus])

  return { status, loading, refresh: fetchStatus }
}
