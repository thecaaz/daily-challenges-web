import { useCallback, useRef, useState } from 'react'

/**
 * Tracks which rows have a request in flight, keyed by any string.
 *
 * `runExclusive(key, task)` ignores further calls for the same key until the
 * first one finishes (also before the re-render that disables the buttons),
 * and `isBusy(key)` lets the row disable its buttons meanwhile.
 *
 * @returns {{ isBusy: (key: string) => boolean, runExclusive: (key: string, task: () => Promise<void>) => Promise<void> }}
 */
export default function useBusyKeys() {
  const [busyKeys, setBusyKeys] = useState(() => new Set())
  const runningKeysRef = useRef(new Set())

  const runExclusive = useCallback(async (key, task) => {
    if (runningKeysRef.current.has(key)) return
    runningKeysRef.current.add(key)
    setBusyKeys(prev => new Set(prev).add(key))
    try {
      await task()
    } finally {
      runningKeysRef.current.delete(key)
      setBusyKeys(prev => {
        const next = new Set(prev)
        next.delete(key)
        return next
      })
    }
  }, [])

  const isBusy = useCallback(key => busyKeys.has(key), [busyKeys])

  return { isBusy, runExclusive }
}
