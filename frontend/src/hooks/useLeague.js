import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import useLatestRequest from './useLatestRequest'

function loadErrorMessage(err) {
  if (err?.response?.status === 404) return 'League not found.'
  return getApiErrorMessage(err, 'Failed to load league.')
}

/**
 * Loads a league by id.
 *
 * - The initial load sets `loading`; if it fails the user is sent back to the
 *   league list.
 * - `refresh()` reloads in the background (after invite, kick, ...): the page
 *   stays rendered and a failure only shows a snackbar.
 * - Only the latest request may update state.
 *
 * @returns {{ league: object|null, loading: boolean, refresh: () => Promise<void>, setLeagueName: (name: string) => void }}
 */
export default function useLeague(leagueId) {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const startRequest = useLatestRequest()
  const [league, setLeague] = useState(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    const isLatest = startRequest()
    setLoading(true)
    try {
      const res = await api.get(`/leagues/${leagueId}`)
      if (isLatest()) setLeague(res.data)
    } catch (err) {
      if (!isLatest()) return
      showSnackbar(loadErrorMessage(err), 'error')
      navigate('/leagues')
    } finally {
      if (isLatest()) setLoading(false)
    }
  }, [leagueId, navigate, showSnackbar, startRequest])

  const refresh = useCallback(async () => {
    const isLatest = startRequest()
    try {
      const res = await api.get(`/leagues/${leagueId}`)
      if (isLatest()) setLeague(res.data)
    } catch (err) {
      if (isLatest()) showSnackbar(getApiErrorMessage(err, 'Failed to refresh league.'), 'error')
    }
  }, [leagueId, showSnackbar, startRequest])

  useEffect(() => { load() }, [load])

  const setLeagueName = useCallback(name => setLeague(prev => ({ ...prev, name })), [])

  return { league, loading, refresh, setLeagueName }
}
