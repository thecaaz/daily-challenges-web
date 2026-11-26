import parseUtcDate from './parseUtcDate'

const MS_PER_MINUTE = 60_000
const MINUTES_PER_HOUR = 60
const HOURS_PER_DAY = 24

export default function timeAgo(value) {
  const date = parseUtcDate(value)
  if (!date || isNaN(date.getTime())) return ''
  const elapsedMs = Date.now() - date.getTime()
  const minutes = Math.floor(elapsedMs / MS_PER_MINUTE)
  if (minutes < 1) return 'just now'
  if (minutes < MINUTES_PER_HOUR) return `${minutes}m ago`
  const hours = Math.floor(minutes / MINUTES_PER_HOUR)
  if (hours < HOURS_PER_DAY) return `${hours}h ago`
  const days = Math.floor(hours / HOURS_PER_DAY)
  return `${days}d ago`
}
