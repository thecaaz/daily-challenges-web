import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api'

const PAGE_SIZE = 12

const replaceItems = (_previous, pageItems) => pageItems
const appendItems = (previous, pageItems) => [...previous, ...pageItems]

/**
 * Per-game play summaries of a league over the last `days` days, loaded page by page.
 *
 * - Changing `leagueId` or `days`, or calling `refresh()`, starts a new
 *   generation: responses of requests from an earlier generation are dropped.
 * - The page counter only advances when a page loaded, so a failed
 *   `loadMore()` can simply be retried.
 * - `loadMore()` is ignored while a request is in flight.
 */
export default function useLeagueGameSummaries(leagueId, initialDays = 7) {
  const [items, setItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [days, setDays] = useState(initialDays)
  const lastLoadedPageRef = useRef(1)
  const generationRef = useRef(0)
  const inFlightRef = useRef(false)

  const fetchPage = useCallback(async ({ page, periodDays, generation, mergeItems }) => {
    if (!leagueId) return
    const isCurrentGeneration = () => generation === generationRef.current
    inFlightRef.current = true
    setLoading(true)
    try {
      const response = await api.get(`/leagues/${leagueId}/game-summaries`, {
        params: { days: periodDays, page, pageSize: PAGE_SIZE },
      })
      if (!isCurrentGeneration()) return
      const pageItems = response.data?.items || []
      setTotalCount(response.data?.totalCount ?? 0)
      setItems(prev => mergeItems(prev, pageItems))
      lastLoadedPageRef.current = page
      setError(null)
    } catch (err) {
      if (isCurrentGeneration()) setError(err)
    } finally {
      if (isCurrentGeneration()) {
        inFlightRef.current = false
        setLoading(false)
      }
    }
  }, [leagueId])

  useEffect(() => {
    const generation = ++generationRef.current
    lastLoadedPageRef.current = 1
    setItems([])
    setTotalCount(0)
    setError(null)
    fetchPage({ page: 1, periodDays: days, generation, mergeItems: replaceItems })
  }, [leagueId, days, fetchPage])

  const loadMore = useCallback(() => {
    if (inFlightRef.current) return
    fetchPage({
      page: lastLoadedPageRef.current + 1,
      periodDays: days,
      generation: generationRef.current,
      mergeItems: appendItems,
    })
  }, [days, fetchPage])

  const refresh = useCallback(() => {
    const generation = ++generationRef.current
    fetchPage({ page: 1, periodDays: days, generation, mergeItems: replaceItems })
  }, [days, fetchPage])

  return { items, totalCount, loading, error, days, setDays, loadMore, refresh }
}
