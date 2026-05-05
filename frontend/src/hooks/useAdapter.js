import { useState, useEffect } from 'react'
import { getAdapterForUrl } from '../utils/adapters'

/**
 * Returns the adapter object for a given game URL, or null if not found / not loaded.
 * Handles async loading with mounted-check cleanup so that state updates are never
 * applied to unmounted components.
 *
 * @param {string|null|undefined} url - The game URL to look up an adapter for.
 * @returns {object|null} The adapter descriptor, or null.
 */
export default function useAdapter(url) {
  const [adapter, setAdapter] = useState(null)

  useEffect(() => {
    if (!url) {
      setAdapter(null)
      return
    }
    let mounted = true
    getAdapterForUrl(url)
      .then(a => { if (mounted) setAdapter(a) })
      .catch(() => { if (mounted) setAdapter(null) })
    return () => { mounted = false }
  }, [url])

  return adapter
}
