import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api'

const PAGE_SIZE = 12

export default function useLeagueGameSummaries(leagueId, initialDays = 7) {
  const [items, setItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [days, setDays] = useState(initialDays)
  const pageRef = useRef(1)
  // Incremented every time we reset (leagueId/days change). Each in-flight
  // request captures the generation at start; if it doesn't match on resolve
  // the response is discarded as stale.
  const genRef = useRef(0)

  const fetchPage = useCallback(async (page, currentDays, append, gen) => {
    if (!leagueId) return
    setLoading(true)
    try {
      const res = await api.get(`/leagues/${leagueId}/game-summaries`, {
        params: { days: currentDays, page, pageSize: PAGE_SIZE },
      })
      if (gen !== genRef.current) return  // stale — a newer fetch superseded us
      const { items: newItems, totalCount: total } = res.data
      setTotalCount(total)
      setItems(prev => append ? [...prev, ...newItems] : newItems)
    } catch {
      // swallow — parent already handles global errors
    } finally {
      if (gen === genRef.current) setLoading(false)
    }
  }, [leagueId])

  // Reset and refetch when leagueId or days changes
  useEffect(() => {
    const gen = ++genRef.current
    pageRef.current = 1
    setItems([])
    setTotalCount(0)
    fetchPage(1, days, false, gen)
  }, [leagueId, days, fetchPage])

  const loadMore = useCallback(() => {
    const nextPage = pageRef.current + 1
    pageRef.current = nextPage
    fetchPage(nextPage, days, true, genRef.current)
  }, [days, fetchPage])

  const refresh = useCallback(() => {
    const gen = ++genRef.current
    pageRef.current = 1
    fetchPage(1, days, false, gen)
  }, [days, fetchPage])

  return { items, totalCount, loading, days, setDays, loadMore, refresh }
}
