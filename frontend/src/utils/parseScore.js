/**
 * Parse a score string into a number, following the shared spec in
 * tests/fixtures/score-parsing.json (also implemented by the backend
 * ScoreParser and extension/scoreparse.js).
 *
 * - The WHOLE trimmed string must be a score; anything else -> NaN.
 * - Optional leading '+'/'-' (numbers only, not times).
 * - Times 'm:ss' and 'h:mm:ss' (optional '.f'/',f', 1-3 digits) -> total seconds.
 * - Grouping separators: ',', '.', space, NBSP, narrow NBSP, ' and ’. The first
 *   group has 1-3 digits, every following group exactly 3.
 * - The last '.'/',' is the decimal point when followed by 1-2 digits, or when
 *   the integer part before it is '0' or empty.
 *
 * Returns NaN (not null) for invalid input so callers can use isNaN().
 */

// Grouping separators: '.', ',', space, NBSP, narrow NBSP, apostrophe, right single quote.
const SEP_CLASS = "[.,   '’]"
const GROUPED = new RegExp(`^\\d{1,3}(?:${SEP_CLASS}\\d{3})+$`)
const SEPARATORS = new RegExp(SEP_CLASS, 'g')
const NUMBER_CHARS = new RegExp(`^(?:\\d|${SEP_CLASS})*$`)
const TIME = /^(\d+):([0-5]\d)(?::([0-5]\d))?(?:[.,](\d{1,3}))?$/

function finite(n) {
  if (!Number.isFinite(n)) return NaN
  return n === 0 ? 0 : n // no -0
}

function parseTime(s) {
  const m = TIME.exec(s)
  if (!m) return NaN
  const total = m[3] !== undefined
    ? Number(m[1]) * 3600 + Number(m[2]) * 60 + Number(m[3])
    : Number(m[1]) * 60 + Number(m[2])
  return finite(Number(String(total) + (m[4] ? '.' + m[4] : '')))
}

export default function parseScore(value) {
  if (value == null) return NaN
  let s = String(value).trim()
  if (s === '') return NaN

  if (s.includes(':')) return parseTime(s)

  let sign = ''
  if (s[0] === '+' || s[0] === '-') {
    sign = s[0] === '-' ? '-' : ''
    s = s.slice(1)
  }
  if (s === '' || !NUMBER_CHARS.test(s)) return NaN

  let intPart = s
  let frac = ''
  const idx = Math.max(s.lastIndexOf('.'), s.lastIndexOf(','))
  if (idx !== -1) {
    const before = s.slice(0, idx)
    const after = s.slice(idx + 1)
    if (/^\d+$/.test(after) && (after.length <= 2 || before === '0' || before === '')) {
      intPart = before === '' ? '0' : before
      frac = after
    }
  }

  let digits
  if (/^\d+$/.test(intPart)) digits = intPart
  else if (GROUPED.test(intPart)) digits = intPart.replace(SEPARATORS, '')
  else return NaN

  return finite(Number(sign + digits + (frac ? '.' + frac : '')))
}
