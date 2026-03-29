// Parse server timestamps as UTC when possible.
// If the string lacks a timezone designator, append `Z` so the Date is interpreted as UTC.
export default function parseUtcDate(input) {
  if (!input) return new Date(NaN)
  if (input instanceof Date) return input
  const s = String(input).trim()

  // If it already contains a timezone designator (Z or +HH:MM / -HH:MM) trust built-in parser
  if (/[zZ]|[+\-]\d{2}:?\d{2}$/.test(s)) return new Date(s)

  // Date-only like "yyyy-mm-dd" -> midnight UTC
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return new Date(`${s}T00:00:00Z`)

  // ISO-like without timezone -> append Z to treat as UTC
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$/.test(s)) return new Date(`${s}Z`)

  // Fallback to Date.parse
  const parsed = Date.parse(s)
  if (!isNaN(parsed)) return new Date(parsed)

  // Last resort - let Date try to parse it
  return new Date(s)
}
