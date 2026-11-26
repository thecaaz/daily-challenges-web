import { useState, useEffect } from 'react'
import { getAdapterForUrl, hasAdapterForUrl, subscribeAdapters } from '../utils/adapters'

/**
 * Returns the adapter object for a given game URL, or null if not found / not loaded.
 * Handles async loading with mounted-check cleanup so that state updates are never
 * applied to unmounted components. Looks the adapter up again when the
 * extension's adapters change (e.g. its content script showed up after the
 * first check on a hard page load).
 *
 * @param {string|null|undefined} url - The game URL to look up an adapter for.
 * @returns {object|null} The adapter descriptor, or null.
 */
export default function useAdapter(url) {
  return useExtensionLookup(url, getAdapterForUrl, null)
}

/**
 * Whether the extension can capture scores for the URL, also asking the
 * extension's own matcher when no adapter descriptor matched. Null while
 * unknown (or without a URL), then true/false. Re-checked when the
 * extension's adapters change.
 *
 * @param {string|null|undefined} url - The game URL; null skips the lookup.
 * @returns {boolean|null}
 */
export function useHasAdapter(url) {
  return useExtensionLookup(url, lookUpHasAdapter, false)
}

function lookUpHasAdapter(url) {
  return hasAdapterForUrl(url).then(Boolean)
}

function useExtensionLookup(url, lookUp, valueOnFailure) {
  const [value, setValue] = useState(null)
  const adaptersVersion = useAdaptersVersion(Boolean(url))

  useEffect(() => {
    if (!url) {
      setValue(null)
      return undefined
    }
    let mounted = true
    lookUp(url)
      .then(found => { if (mounted) setValue(found) })
      .catch(() => { if (mounted) setValue(valueOnFailure) })
    return () => { mounted = false }
  }, [url, lookUp, valueOnFailure, adaptersVersion])

  return value
}

/** Bumped when the extension's adapters change (e.g. it showed up late). */
function useAdaptersVersion(isSubscribed) {
  const [version, setVersion] = useState(0)

  useEffect(() => {
    if (!isSubscribed) return undefined
    return subscribeAdapters(() => setVersion(v => v + 1))
  }, [isSubscribed])

  return version
}
