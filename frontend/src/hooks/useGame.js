import { useState, useEffect, useCallback } from 'react'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import useLatestRequest from './useLatestRequest'
import { isNotFoundError } from '../api/httpStatus'

/**
 * Loads a game's overview (game, available dates, whether the current user has
 * submitted for the latest day).
 *
 * - Re-fetches when the logged-in user changes (the overview depends on who asks).
 * - A 404 (or an empty overview) sets `notFound`; any other failure sets `error`
 *   instead of throwing, so callers can show an error state rather than an
 *   endless spinner.
 * - Responses from superseded requests are ignored.
 */
export default function useGame(gameId) {
  const auth = useAuth()
  const userId = auth?.user?.id ?? null

  const [game, setGame] = useState(null)
  const [availableDates, setAvailableDates] = useState([])
  const [hasSubmittedForLatest, setHasSubmittedForLatest] = useState(false)
  const [notFound, setNotFound] = useState(false)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(true)
  const startRequest = useLatestRequest()

  const fetchOverview = useCallback(async () => {
    const isLatest = startRequest()
    if (!gameId) {
      setLoading(false)
      return
    }
    setLoading(true)
    setNotFound(false)
    setError(null)

    const markNotFound = () => {
      setNotFound(true)
      setGame(null)
      setAvailableDates([])
      setHasSubmittedForLatest(false)
    }

    try {
      const response = await api.get(`/games/${gameId}/overview?include=availableDates,hasSubmitted`)
      if (!isLatest()) return
      const overview = response?.data || {}
      if (!overview.game) {
        markNotFound()
        return
      }
      setGame(overview.game)
      setAvailableDates(overview.availableDates || [])
      setHasSubmittedForLatest(!!overview.hasSubmittedForLatest)
    } catch (err) {
      if (!isLatest()) return
      if (isNotFoundError(err)) markNotFound()
      else setError(err || new Error('Failed to load game'))
    } finally {
      if (isLatest()) setLoading(false)
    }
  }, [gameId, startRequest])

  useRefetchWhenViewerChanges(fetchOverview, userId)

  return { game, availableDates, hasSubmittedForLatest, notFound, error, loading, refresh: fetchOverview }
}

/** hasSubmittedForLatest depends on who asks, so log-in and log-out refetch. */
function useRefetchWhenViewerChanges(fetchOverview, userId) {
  useEffect(() => {
    fetchOverview()
  }, [fetchOverview, userId])
}
