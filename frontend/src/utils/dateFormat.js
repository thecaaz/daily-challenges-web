import parseUtcDate from './parseUtcDate'

export function formatDateTime(dateStr) {
  return parseUtcDate(dateStr).toLocaleString()
}

export function formatDateTimeUtc(dateStr) {
  return parseUtcDate(dateStr).toLocaleString(undefined, { timeZone: 'UTC' })
}
