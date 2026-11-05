import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

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
  const requestRef = useRef(0)

  const fetchOverview = useCallback(async () => {
    const requestId = ++requestRef.current
    if (!gameId) {
      setLoading(false)
      return
    }
    setLoading(true)
    setNotFound(false)
    setError(null)

    const clear = () => {
      setGame(null)
      setAvailableDates([])
      setHasSubmittedForLatest(false)
    }

    try {
      const overviewRes = await api.get(`/games/${gameId}/overview?include=availableDates,hasSubmitted`)
      if (requestId !== requestRef.current) return
      const overview = overviewRes?.data || {}

      if (!overview.game) {
        setNotFound(true)
        clear()
      } else {
        setGame(overview.game)
        setAvailableDates(overview.availableDates || [])
        setHasSubmittedForLatest(!!overview.hasSubmittedForLatest)
      }
    } catch (err) {
      if (requestId !== requestRef.current) return
      if (err?.response?.status === 404) {
        setNotFound(true)
        clear()
      } else {
        setError(err || new Error('Failed to load game'))
      }
    } finally {
      if (requestId === requestRef.current) setLoading(false)
    }
  }, [gameId])

  // userId is a real dependency: hasSubmittedForLatest depends on who asks, so
  // re-fetch when the user logs in or out.
  useEffect(() => {
    fetchOverview()
  }, [fetchOverview, userId])

  return { game, availableDates, hasSubmittedForLatest, notFound, error, loading, refresh: fetchOverview }
}
