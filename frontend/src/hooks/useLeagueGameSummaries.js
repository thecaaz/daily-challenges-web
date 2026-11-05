import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api'

const PAGE_SIZE = 12

export default function useLeagueGameSummaries(leagueId, initialDays = 7) {
  const [items, setItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [days, setDays] = useState(initialDays)
  // Last page that loaded successfully. Only advanced on success, so a failed
  // "Load more" can simply be retried.
  const pageRef = useRef(1)
  // Incremented every time we reset (leagueId/days change, refresh). Each
  // in-flight request captures the generation at start; if it doesn't match on
  // resolve the response is discarded as stale.
  const genRef = useRef(0)
  const inFlightRef = useRef(false)

  const fetchPage = useCallback(async (page, currentDays, append, gen) => {
    if (!leagueId) return
    inFlightRef.current = true
    setLoading(true)
    try {
      const res = await api.get(`/leagues/${leagueId}/game-summaries`, {
        params: { days: currentDays, page, pageSize: PAGE_SIZE },
      })
      if (gen !== genRef.current) return  // stale — a newer fetch superseded us
      const newItems = res.data?.items || []
      setTotalCount(res.data?.totalCount ?? 0)
      setItems(prev => append ? [...prev, ...newItems] : newItems)
      pageRef.current = page
      setError(null)
    } catch (err) {
      if (gen === genRef.current) setError(err)
    } finally {
      if (gen === genRef.current) {
        inFlightRef.current = false
        setLoading(false)
      }
    }
  }, [leagueId])

  // Reset and refetch when leagueId or days changes
  useEffect(() => {
    const gen = ++genRef.current
    pageRef.current = 1
    setItems([])
    setTotalCount(0)
    setError(null)
    fetchPage(1, days, false, gen)
  }, [leagueId, days, fetchPage])

  const loadMore = useCallback(() => {
    if (inFlightRef.current) return
    fetchPage(pageRef.current + 1, days, true, genRef.current)
  }, [days, fetchPage])

  const refresh = useCallback(() => {
    const gen = ++genRef.current
    fetchPage(1, days, false, gen)
  }, [days, fetchPage])

  return { items, totalCount, loading, error, days, setDays, loadMore, refresh }
}
