export default function formatNumber(value) {
  if (value === null || value === undefined || value === '') return ''
  // If it's already a number, format directly
  if (typeof value === 'number') return value.toLocaleString()
  // Attempt to coerce numeric strings to numbers
  const n = Number(value)
  if (!Number.isFinite(n)) return value
  return n.toLocaleString()
}
