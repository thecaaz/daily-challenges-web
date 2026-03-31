/**
 * Try to parse a score string into an integer, stripping thousand separators
 * (commas, periods, and spaces). Returns the integer if valid, or NaN.
 *
 * Supports formats like: "1,000", "1.000.000", "1 000", "1,000,000"
 */
export default function parseScore(value) {
  if (value == null || value === '') return NaN
  const stripped = String(value).replace(/[,.\s]/g, '')
  if (stripped === '' || !/^-?\d+$/.test(stripped)) return NaN
  return parseInt(stripped, 10)
}
