import { useState, useEffect, useRef } from 'react'
import { Typography } from '@mui/material'
import parseUtcDate from '../utils/parseUtcDate'

function pad(n) {
  return String(n).padStart(2, '0')
}

// Small grace period after the reset before `onExpire` fires, so the server has
// rolled over to the new scoring day when the parent refetches.
const EXPIRE_GRACE_MS = 1500

/**
 * Counts down to `targetDate` (UTC). Calls `onExpire` once per target date when
 * the countdown reaches zero, so the parent can refetch the game.
 */
export default function Countdown({ targetDate, onExpire }) {
  const [remaining, setRemaining] = useState(null)
  const onExpireRef = useRef(onExpire)

  useEffect(() => {
    onExpireRef.current = onExpire
  }, [onExpire])

  useEffect(() => {
    const target = parseUtcDate(targetDate)
    if (isNaN(target.getTime())) {
      setRemaining(null)
      return
    }

    let interval = null
    let expireTimer = null
    const tick = () => {
      const diff = target.getTime() - Date.now()
      if (diff <= 0) {
        setRemaining(0)
        if (interval) clearInterval(interval)
        interval = null
        // Fire once for this target date only (a stale target won't loop).
        if (!expireTimer) {
          expireTimer = setTimeout(() => { onExpireRef.current?.() }, EXPIRE_GRACE_MS)
        }
        return
      }
      setRemaining(diff)
    }

    interval = setInterval(tick, 1000)
    tick()
    return () => {
      if (interval) clearInterval(interval)
      if (expireTimer) clearTimeout(expireTimer)
    }
  }, [targetDate])

  if (remaining === null) return null

  if (remaining === 0) {
    return (
      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)' }}>
        Reset complete!
      </Typography>
    )
  }

  const totalSeconds = Math.floor(remaining / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  return (
    <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)' }}>
      Resets in {pad(hours)}:{pad(minutes)}:{pad(seconds)}
    </Typography>
  )
}
