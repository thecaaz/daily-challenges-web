/**
 * Format a number for display with the user's locale grouping.
 *
 * Strings are formatted only when they are plain integers ("40456" ->
 * "40,456"). Any other string (scores as the user typed them: "40.456",
 * "2:59", "1 234,5") is returned unchanged, because re-reading it as a JS
 * number would misinterpret locale grouping (e.g. "40.456" means 40456).
 */
export default function formatNumber(value) {
  if (value === null || value === undefined || value === '') return ''
  if (typeof value === 'number') return Number.isFinite(value) ? value.toLocaleString() : ''
  const s = String(value).trim()
  if (/^-?\d{1,15}$/.test(s)) return Number(s).toLocaleString()
  return s
}
