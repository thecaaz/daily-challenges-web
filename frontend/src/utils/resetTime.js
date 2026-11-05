// Reset-time conversion between the API's UTC "HH:mm" and the admin's local time.
//
// `offsetMin` follows Date#getTimezoneOffset semantics: minutes to ADD to local
// time to get UTC (UTC+1 -> -60, UTC-5 -> 300). It is a parameter so callers
// and tests don't depend on the machine's timezone.

const MINUTES_PER_DAY = 24 * 60

function parseHHmm(hhmm) {
  const m = /^(\d{1,2}):(\d{2})(?::\d{2})?$/.exec(String(hhmm ?? '').trim())
  if (!m) return null
  const h = Number(m[1])
  const min = Number(m[2])
  if (h > 23 || min > 59) return null
  return h * 60 + min
}

function formatMinutes(total) {
  const wrapped = ((total % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY
  const h = Math.floor(wrapped / 60)
  const m = wrapped % 60
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`
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
