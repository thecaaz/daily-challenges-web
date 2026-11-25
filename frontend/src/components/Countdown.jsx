import { useState, useEffect, useRef } from 'react'
import { Typography } from '@mui/material'
import parseUtcDate from '../utils/parseUtcDate'

const TICK_MS = 1000
const SECONDS_PER_HOUR = 3600
const SECONDS_PER_MINUTE = 60
const captionSx = { color: 'rgba(255,255,255,0.7)' }

/**
 * Small grace period after the reset before `onExpire` fires, so the server has
 * rolled over to the new scoring day when the parent refetches.
 */
const EXPIRE_GRACE_MS = 1500

function pad(n) {
  return String(n).padStart(2, '0')
}

function formatHms(milliseconds) {
  const totalSeconds = Math.floor(milliseconds / TICK_MS)
  const hours = Math.floor(totalSeconds / SECONDS_PER_HOUR)
  const minutes = Math.floor((totalSeconds % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE)
  const seconds = totalSeconds % SECONDS_PER_MINUTE
  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
}

/**
 * Counts down to `targetDate` (UTC). Calls `onExpire` once per target date when
 * the countdown reaches zero, so the parent can refetch the game; a stale
 * target never loops.
 */
export default function Countdown({ targetDate, onExpire }) {
  const remaining = useRemainingTime(targetDate, onExpire)

  if (remaining === null) return null

  if (remaining === 0) {
    return (
      <Typography variant="caption" sx={captionSx}>
        Reset complete!
      </Typography>
    )
  }

  return (
    <Typography variant="caption" sx={captionSx}>
      Resets in {formatHms(remaining)}
    </Typography>
  )
}

/** Milliseconds until `targetDate` (0 once reached), or null when it is invalid. */
function useRemainingTime(targetDate, onExpire) {
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
    const stopTicking = () => {
      if (interval) clearInterval(interval)
      interval = null
    }
    const scheduleExpireOnce = () => {
      if (!expireTimer) expireTimer = setTimeout(() => { onExpireRef.current?.() }, EXPIRE_GRACE_MS)
    }
    const tick = () => {
      const diff = target.getTime() - Date.now()
      if (diff > 0) {
        setRemaining(diff)
        return
      }
      setRemaining(0)
      stopTicking()
      scheduleExpireOnce()
    }

    interval = setInterval(tick, TICK_MS)
    tick()
    return () => {
      stopTicking()
      if (expireTimer) clearTimeout(expireTimer)
    }
  }, [targetDate])

  return remaining
}
