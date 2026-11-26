import { useCallback, useEffect, useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import downloadBlob from '../utils/downloadBlob'
import { getApiErrorMessage } from '../utils/apiError'
import useLatestRequest from './useLatestRequest'

export const XP_EVENTS_PAGE_SIZE = 20

const EMPTY_FILTERS = { from: '', to: '', eventType: '' }

/** "yyyy-MM-dd" -> the next day as "yyyy-MM-dd" (calendar arithmetic, timezone-free). */
export function nextDay(yyyyMmDd) {
  const date = new Date(`${yyyyMmDd}T00:00:00Z`)
  if (Number.isNaN(date.getTime())) return null
  date.setUTCDate(date.getUTCDate() + 1)
  return date.toISOString().slice(0, 10)
}

/**
 * Query params for the XP-event endpoints. The "To" date picked in the UI is
 * inclusive, while the API's `to` is exclusive, so the day after is sent.
 */
export function buildFilterParams({ from, to, eventType }) {
  const params = {}
  if (from) params.from = from
  if (to) {
    const end = nextDay(to)
    if (end) params.to = end
  }
  if (eventType) params.eventType = eventType
  return params
}

/**
 * Paged, filtered XP events of one user for the admin audit dialog.
 *
 * - Opening (or switching user) clears the filters and loads page 1.
 * - `filters` are the values being edited; `apply()` loads them. Paging and
 *   CSV export reuse the filters of the rows on screen, so editing a field
 *   without pressing Apply doesn't change what Prev/Next/Export show.
 * - Only the latest page request may update state.
 */
export default function useXpEventAudit({ open, userId }) {
  const { showSnackbar } = useSnackbar()
  const startRequest = useLatestRequest()
  const [items, setItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState(EMPTY_FILTERS)
  const [appliedFilters, setAppliedFilters] = useState(EMPTY_FILTERS)

  const loadPage = useCallback(async (pageToLoad, filtersToApply) => {
    if (!userId) return
    const isLatest = startRequest()
    try {
      const params = { page: pageToLoad, pageSize: XP_EVENTS_PAGE_SIZE, ...buildFilterParams(filtersToApply) }
      const response = await api.get(`/admin/users/${userId}/xp-events`, { params })
      if (!isLatest()) return
      setItems(response.data?.items || [])
      setTotalCount(response.data?.totalCount || 0)
      setPage(pageToLoad)
      setAppliedFilters(filtersToApply)
    } catch (err) {
      if (isLatest()) showSnackbar(getApiErrorMessage(err, 'Failed to load XP events'), 'error')
    }
  }, [userId, showSnackbar, startRequest])

  useEffect(() => {
    if (!open) return
    setFilters(EMPTY_FILTERS)
    setItems([])
    setTotalCount(0)
    loadPage(1, EMPTY_FILTERS)
  }, [open, loadPage])

  const setFilter = (name, value) => setFilters(prev => ({ ...prev, [name]: value }))

  const reset = () => {
    setFilters(EMPTY_FILTERS)
    loadPage(1, EMPTY_FILTERS)
  }

  const exportCsv = async () => {
    try {
      const params = buildFilterParams(appliedFilters)
      const response = await api.get(`/admin/users/${userId}/xp-events/export`, { params, responseType: 'blob' })
      downloadBlob(`xp-events-user-${userId}.csv`, new Blob([response.data]))
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to export CSV'), 'error')
    }
  }

  return {
    items,
    totalCount,
    filters,
    setFilter,
    apply: () => loadPage(1, filters),
    reset,
    hasPreviousPage: page > 1,
    hasNextPage: page * XP_EVENTS_PAGE_SIZE < totalCount,
    goToPreviousPage: () => loadPage(page - 1, appliedFilters),
    goToNextPage: () => loadPage(page + 1, appliedFilters),
    exportCsv,
  }
}
