import { useState, useEffect } from 'react'
import { Typography } from '@mui/material'
import parseUtcDate from '../utils/parseUtcDate'

function pad(n) {
  return String(n).padStart(2, '0')
}

export default function Countdown({ targetDate }) {
  const [remaining, setRemaining] = useState(null)

  useEffect(() => {
    const target = parseUtcDate(targetDate)
    if (isNaN(target.getTime())) {
      setRemaining(null)
      return
    }

    const tick = () => {
      const diff = target.getTime() - Date.now()
      if (diff <= 0) {
        setRemaining(0)
        return
      }
      setRemaining(diff)
    }

    tick()
    const interval = setInterval(tick, 1000)
    return () => clearInterval(interval)
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
