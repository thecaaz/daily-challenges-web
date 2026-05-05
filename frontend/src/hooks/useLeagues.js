import { useState, useEffect, useCallback } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'

export default function useLeagues() {
  const [leagues, setLeagues] = useState([])
  const [pendingInvitations, setPendingInvitations] = useState([])
  const [loading, setLoading] = useState(true)
  const { showSnackbar } = useSnackbar()

  const fetchAll = useCallback(async () => {
    setLoading(true)
    try {
      const [leaguesRes, invRes] = await Promise.all([
        api.get('/leagues'),
        api.get('/leagues/invitations/pending'),
      ])
      setLeagues(leaguesRes.data)
      setPendingInvitations(invRes.data)
    } catch {
      showSnackbar('Failed to load leagues.', 'error')
    } finally {
      setLoading(false)
    }
  }, [showSnackbar])

  useEffect(() => { fetchAll() }, [fetchAll])

  return { leagues, pendingInvitations, loading, refresh: fetchAll }
}
