import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api'

const PAGE_SIZE = 12

export default function useLeagueGameSummaries(leagueId, initialDays = 7) {
  const [items, setItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [days, setDays] = useState(initialDays)
  const pageRef = useRef(1)
  const activeRef = useRef(true)

  const fetchPage = useCallback(async (page, currentDays, append = false) => {
    if (!leagueId) return
    setLoading(true)
    try {
      const res = await api.get(`/leagues/${leagueId}/game-summaries`, {
        params: { days: currentDays, page, pageSize: PAGE_SIZE },
      })
      if (!activeRef.current) return
      const { items: newItems, totalCount: total } = res.data
      setTotalCount(total)
      setItems(prev => append ? [...prev, ...newItems] : newItems)
    } catch {
      // swallow — parent already handles global errors
    } finally {
      if (activeRef.current) setLoading(false)
    }
  }, [leagueId])

  // Reset and refetch when leagueId or days changes
  useEffect(() => {
    activeRef.current = true
    pageRef.current = 1
    setItems([])
    setTotalCount(0)
    fetchPage(1, days, false)
    return () => { activeRef.current = false }
  }, [leagueId, days, fetchPage])

  const loadMore = useCallback(() => {
    const nextPage = pageRef.current + 1
    pageRef.current = nextPage
    fetchPage(nextPage, days, true)
  }, [days, fetchPage])

  const refresh = useCallback(() => {
    pageRef.current = 1
    fetchPage(1, days, false)
  }, [days, fetchPage])

  return { items, totalCount, loading, days, setDays, loadMore, refresh }
}
