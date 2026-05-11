import { useState, useEffect, useCallback } from 'react'

/**
 * Generic async data fetching hook with mounted-check cleanup and refetch support.
 *
 * @param {() => Promise<T>} fetcher - Function returning a promise resolving to the data.
 * @param {Array} deps - Dependencies that trigger a re-fetch (like useEffect deps).
 * @returns {{ data: T|null, loading: boolean, error: boolean, refetch: () => void }}
 */
export default function useAsyncData(fetcher, deps = []) {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [epoch, setEpoch] = useState(0)

  useEffect(() => {
    let mounted = true
    setLoading(true)
    setError(false)
    fetcher()
      .then(result => { if (mounted) { setData(result); setLoading(false) } })
      .catch(() => { if (mounted) { setError(true); setLoading(false) } })
    return () => { mounted = false }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [epoch, ...deps])

  const refetch = useCallback(() => setEpoch(e => e + 1), [])

  return { data, loading, error, refetch }
}
