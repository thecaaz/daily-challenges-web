import { useCallback, useRef } from 'react'

/**
 * Tracks the latest of several overlapping async requests.
 *
 * `startRequest()` marks a new request as the latest and returns an
 * `isLatest()` check, so a response can tell whether a newer request has
 * superseded it before writing state.
 *
 * @returns {() => (() => boolean)} startRequest
 */
export default function useLatestRequest() {
  const latestIdRef = useRef(0)

  return useCallback(() => {
    const requestId = ++latestIdRef.current
    return () => requestId === latestIdRef.current
  }, [])
}
