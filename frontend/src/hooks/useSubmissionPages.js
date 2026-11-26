import { useCallback, useEffect, useState } from 'react'
import api from '../api'
import useLatestRequest from './useLatestRequest'
import { isNotFoundError } from '../api/httpStatus'

const PAGE_SIZE = 50
const EMPTY_PAGES = { submissions: [], page: 1, hasMore: false, scoresHidden: false }

async function getSubmissionsPage(gameId, page, scoringDay) {
  let url = `/submissions/game/${gameId}?page=${page}&pageSize=${PAGE_SIZE}`
  if (scoringDay) url += `&scoringDay=${encodeURIComponent(scoringDay)}`
  const res = await api.get(url)
  return res?.data || { items: [], hasMore: false }
}

function pageHasMore(pageResult) {
  if (typeof pageResult.totalPages === 'number') return pageResult.page < pageResult.totalPages
  return pageResult.hasMore === true
}

/**
 * Appends a page, skipping items already shown: rows can shift between pages
 * when new submissions arrive while paging.
 */
export function appendUniqueById(prev, items) {
  const seen = new Set(prev.map(s => s.id))
  const fresh = (items || []).filter(s => {
    if (s?.id == null) return true
    if (seen.has(s.id)) return false
    seen.add(s.id)
    return true
  })
  return fresh.length ? [...prev, ...fresh] : prev
}

/**
 * A game's submissions for one scoring day (`''` = all days), loaded page by page.
 *
 * - The list resets whenever the game, the day, `enabled` or a `retry()`
 *   changes it; `listKey` identifies the current list.
 * - While not `enabled` the list stays empty and nothing is requested.
 * - A 404 sets `isNotFound`; any other failure sets `hasError`.
 * - Only the latest request may update state.
 *
 * @param {{ gameId: string, scoringDay: string, enabled: boolean }} query
 */
export default function useSubmissionPages({ gameId, scoringDay, enabled }) {
  const startRequest = useLatestRequest()
  const [pages, setPages] = useState(EMPTY_PAGES)
  const [isLoading, setIsLoading] = useState(false)
  const [hasError, setHasError] = useState(false)
  const [isNotFound, setIsNotFound] = useState(false)
  const [reloadCount, setReloadCount] = useState(0)

  useEffect(() => {
    const isLatest = startRequest()
    setPages(EMPTY_PAGES)
    setHasError(false)
    setIsNotFound(false)
    setIsLoading(enabled)
    if (!enabled) return

    getSubmissionsPage(gameId, 1, scoringDay || undefined)
      .then(pageResult => {
        if (!isLatest()) return
        setPages({
          submissions: pageResult.items || [],
          page: 1,
          hasMore: pageHasMore(pageResult),
          scoresHidden: !!pageResult.scoresHidden,
        })
      })
      .catch(err => {
        if (!isLatest()) return
        if (isNotFoundError(err)) setIsNotFound(true)
        else setHasError(true)
      })
      .finally(() => {
        if (isLatest()) setIsLoading(false)
      })
  }, [gameId, scoringDay, enabled, reloadCount, startRequest])

  const loadMore = useCallback(async () => {
    if (isLoading) return
    const isLatest = startRequest()
    const nextPage = pages.page + 1
    setIsLoading(true)
    try {
      const pageResult = await getSubmissionsPage(gameId, nextPage, scoringDay || undefined)
      if (!isLatest()) return
      setPages(prev => ({
        submissions: appendUniqueById(prev.submissions, pageResult.items),
        page: nextPage,
        hasMore: pageHasMore(pageResult),
        scoresHidden: prev.scoresHidden || !!pageResult.scoresHidden,
      }))
    } catch {
      if (isLatest()) setHasError(true)
    } finally {
      if (isLatest()) setIsLoading(false)
    }
  }, [gameId, isLoading, pages.page, scoringDay, startRequest])

  const retry = useCallback(() => setReloadCount(count => count + 1), [])

  return {
    ...pages,
    isLoading,
    hasError,
    isNotFound,
    listKey: `${gameId}|${scoringDay}|${enabled}|${reloadCount}`,
    loadMore,
    retry,
  }
}
