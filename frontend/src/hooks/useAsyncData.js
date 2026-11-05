import { useState, useEffect, useCallback } from 'react'

function sameDeps(a, b) {
  if (a.length !== b.length) return false
  for (let i = 0; i < a.length; i++) {
    if (!Object.is(a[i], b[i])) return false
  }
  return true
}

/**
 * Generic async data fetching hook with stale-response protection and refetch support.
 *
 * - When `deps` change, `data` is reset to null (and `loading` set) in the same
 *   render, so callers never show the previous key's data under the new key.
 * - A failed fetch resets `data` to null.
 * - Responses from superseded requests (older deps, earlier refetch, unmounted)
 *   are ignored.
 * - `refetch()` keeps the current data visible while reloading.
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
  const [prevDeps, setPrevDeps] = useState(deps)

  // Reset during render when the deps change (React's "adjust state on prop
  // change" pattern) so the stale data is never rendered with the new deps.
  if (!sameDeps(prevDeps, deps)) {
    setPrevDeps(deps)
    setData(null)
    setLoading(true)
    setError(false)
  }

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(false)
    let request
    try {
      request = Promise.resolve(fetcher())
    } catch (err) {
      request = Promise.reject(err)
    }
    request
      .then(result => {
        if (!active) return
        setData(result)
        setLoading(false)
      })
      .catch(() => {
        if (!active) return
        setData(null)
        setError(true)
        setLoading(false)
      })
    return () => { active = false }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [epoch, ...deps])

  const refetch = useCallback(() => setEpoch(e => e + 1), [])

  return { data, loading, error, refetch }
}
