import { useEffect, useRef, useState } from 'react'

const FLASH_DURATION_MS = 2500

/**
 * The XP gained by the latest increase of `totalXp`, shown for 2.5 s.
 *
 * Returns `{ amount, id }` while flashing (a new `id` per gain, usable as a
 * React key to restart the animation) and null otherwise. The first value
 * and decreases never flash; `totalXp` is null while logged out.
 *
 * @param {number|null} totalXp
 * @returns {{ amount: number, id: number }|null}
 */
export default function useXpGainFlash(totalXp) {
  const previousXpRef = useRef(null)
  const gainIdRef = useRef(0)
  const [gain, setGain] = useState(null)

  useEffect(() => {
    const previousXp = previousXpRef.current
    previousXpRef.current = totalXp
    const hasGained = totalXp !== null && previousXp !== null && totalXp > previousXp
    if (!hasGained) return undefined
    gainIdRef.current += 1
    setGain({ amount: totalXp - previousXp, id: gainIdRef.current })
    const timer = setTimeout(() => setGain(null), FLASH_DURATION_MS)
    return () => {
      clearTimeout(timer)
      setGain(null)
    }
  }, [totalXp])

  return gain
}
