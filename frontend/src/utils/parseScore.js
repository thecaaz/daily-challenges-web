/**
 * Try to parse a score string into a number, handling thousand separators
 * and decimal separators across different locales.
 *
 * The last period or comma followed by fewer than 3 digits is treated as
 * the decimal separator; all other periods/commas/spaces are thousand
 * separators and get stripped.
 *
 * Supports: "1,000", "1.000.000", "1 000", "1,000,000", "3.14", "3,14",
 *           "1.234,56", "1 234 567,89", "1,234,567.89"
 */
export default function parseScore(value) {
  if (value == null || value === '') return NaN

  let s = String(value).trim()
  if (s === '') return NaN

  let sign = ''
  if (s.startsWith('-') || s.startsWith('+')) {
    sign = s[0] === '-' ? '-' : ''
    s = s.slice(1)
  }

  // Find the last separator (comma or period)
  let lastSepIdx = -1
  for (let i = s.length - 1; i >= 0; i--) {
    if (s[i] === ',' || s[i] === '.') {
      lastSepIdx = i
      break
    }
  }

  if (lastSepIdx === -1) {
    // No separators found — just digits (and optional sign)
    if (!/^\d+$/.test(s)) return NaN
    return parseFloat(sign + s)
  }

  let beforeSep = s.slice(0, lastSepIdx)
  let afterSep = s.slice(lastSepIdx + 1)

  // If nothing after the last separator, treat it as a thousand separator
  if (afterSep.length === 0) {
    afterSep = '0'
  }

  // If followed by fewer than 3 digits, this is the decimal separator
  if (afterSep.length < 3) {
    // Strip remaining thousand separators from before part
    beforeSep = beforeSep.replace(/[,.\s]/g, '')
    if (!/^\d+$/.test(beforeSep)) return NaN
    let numStr = sign + beforeSep + '.' + afterSep
    let result = parseFloat(numStr)
    return Number.isFinite(result) ? result : NaN
  }

  // Last separator is a thousand separator — strip all separators and parse
  let stripped = (sign + s).replace(/[,.\s]/g, '')
  if (!/^-?\d+$/.test(stripped)) return NaN
  return parseFloat(stripped)
}
