const HAS_TIMEZONE = /[zZ]|[+-]\d{2}:?\d{2}$/
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/
const ISO_WITHOUT_TIMEZONE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$/

/**
 * Parse server timestamps as UTC when possible. An explicit timezone (Z or
 * +HH:MM / -HH:MM) is trusted; a date-only value is midnight UTC; an ISO
 * timestamp without timezone gets a `Z`; anything else goes to Date.parse,
 * then to the Date constructor.
 */
export default function parseUtcDate(input) {
  if (!input) return new Date(NaN)
  if (input instanceof Date) return input
  const s = String(input).trim()

  if (HAS_TIMEZONE.test(s)) return new Date(s)
  if (DATE_ONLY.test(s)) return new Date(`${s}T00:00:00Z`)
  if (ISO_WITHOUT_TIMEZONE.test(s)) return new Date(`${s}Z`)

  const parsed = Date.parse(s)
  if (!isNaN(parsed)) return new Date(parsed)
  return new Date(s)
}
