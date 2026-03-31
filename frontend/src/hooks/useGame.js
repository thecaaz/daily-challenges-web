import { useState, useEffect, useCallback } from 'react'
import api from '../api'

export default function useGame(gameId) {
  const [game, setGame] = useState(null)
  const [availableDates, setAvailableDates] = useState([])
  const [hasSubmittedForLatest, setHasSubmittedForLatest] = useState(false)
  const [notFound, setNotFound] = useState(false)
  const [loading, setLoading] = useState(true)

  const fetchOverview = useCallback(async () => {
    if (!gameId) return
    setLoading(true)
    setNotFound(false)
    try {
      // Preferred aggregated overview endpoint which includes availableDates and hasSubmitted
      const overviewRes = await api.get(`/games/${gameId}/overview?include=availableDates,hasSubmitted`)
      const overview = overviewRes.data || {}

      if (!overview.game) {
        setNotFound(true)
        setGame(null)
        setAvailableDates([])
        setHasSubmittedForLatest(false)
      } else {
        setGame(overview.game)
        setAvailableDates(overview.availableDates || [])
        setHasSubmittedForLatest(!!overview.hasSubmittedForLatest)
      }
    } catch (err) {
      if (err?.response?.status === 404) {
        setNotFound(true)
        setGame(null)
        setAvailableDates([])
        setHasSubmittedForLatest(false)
      } else {
        throw err
      }
    } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => {
    fetchOverview()
  }, [fetchOverview])

  const refresh = fetchOverview

  return { game, availableDates, hasSubmittedForLatest, notFound, loading, refresh }
}
