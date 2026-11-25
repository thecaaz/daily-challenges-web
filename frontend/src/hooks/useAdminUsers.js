import { useEffect, useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import useAsyncData from './useAsyncData'
import useDebouncedValue from './useDebouncedValue'

const SEARCH_DEBOUNCE_MS = 300
const DEFAULT_PAGE_SIZE = 50

async function getUsersPage({ page, pageSize, search }) {
  const params = { page, pageSize }
  if (search) params.search = search
  const res = await api.get('/admin/users', { params })
  return res.data
}

/**
 * One page of users for the admin user list, with a debounced username search.
 *
 * - Nothing is loaded until `isAuthorized`.
 * - A new search term or page size starts again at page 1 (pages are 1-based).
 * - `totalCount` keeps the last known total while the next page loads, so
 *   the pagination control doesn't jump back to 0 rows.
 */
export default function useAdminUsers(isAuthorized) {
  const { showSnackbar } = useSnackbar()
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)
  const [searchInput, setSearchInput] = useState('')
  const search = useDebouncedValue(searchInput.trim(), SEARCH_DEBOUNCE_MS)
  const [searchedFor, setSearchedFor] = useState(search)
  const [totalCount, setTotalCount] = useState(0)

  if (search !== searchedFor) {
    setSearchedFor(search)
    setPage(1)
  }

  const { data, loading, error, refetch } = useAsyncData(
    () => (isAuthorized ? getUsersPage({ page, pageSize, search }) : null),
    [isAuthorized, page, pageSize, search]
  )

  const loadedTotal = data ? (data.totalCount || 0) : totalCount
  if (loadedTotal !== totalCount) setTotalCount(loadedTotal)

  useEffect(() => {
    if (error) showSnackbar('Failed to load users', 'error')
  }, [error, showSnackbar])

  const changePageSize = newPageSize => {
    setPageSize(newPageSize)
    setPage(1)
  }

  return {
    users: data?.items || [],
    loading,
    error,
    refresh: refetch,
    searchInput,
    setSearchInput,
    page,
    setPage,
    pageSize,
    changePageSize,
    totalCount,
  }
}
