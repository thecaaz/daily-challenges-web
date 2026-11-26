// Reset-time conversion between the API's UTC "HH:mm" and the admin's local time.
//
// `offsetMin` follows Date#getTimezoneOffset semantics: minutes to ADD to local
// time to get UTC (UTC+1 -> -60, UTC-5 -> 300). It is a parameter so callers
// and tests don't depend on the machine's timezone.

const MINUTES_PER_HOUR = 60
const MINUTES_PER_DAY = 24 * MINUTES_PER_HOUR

function parseHHmm(hhmm) {
  const match = /^(\d{1,2}):(\d{2})(?::\d{2})?$/.exec(String(hhmm ?? '').trim())
  if (!match) return null
  const hours = Number(match[1])
  const minutes = Number(match[2])
  if (hours > 23 || minutes > 59) return null
  return hours * MINUTES_PER_HOUR + minutes
}

function formatMinutes(total) {
  const wrapped = ((total % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY
  const hours = Math.floor(wrapped / MINUTES_PER_HOUR)
  const minutes = wrapped % MINUTES_PER_HOUR
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}

/** Current browser offset (Date#getTimezoneOffset semantics). */
export function currentOffsetMinutes(date = new Date()) {
  return date.getTimezoneOffset()
}

/** UTC "HH:mm" -> local "HH:mm". Returns null for invalid input. */
export function utcToLocalHHmm(hhmm, offsetMin = currentOffsetMinutes()) {
  const total = parseHHmm(hhmm)
  if (total == null) return null
  return formatMinutes(total - offsetMin)
}

/** Local "HH:mm" -> UTC "HH:mm". Returns null for invalid input. */
export function localToUtcHHmm(hhmm, offsetMin = currentOffsetMinutes()) {
  const total = parseHHmm(hhmm)
  if (total == null) return null
  return formatMinutes(total + offsetMin)
}
