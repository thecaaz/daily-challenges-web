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

/** Grouping separators: '.', ',', space, NBSP, narrow NBSP, apostrophe, right single quote. */
const SEPARATOR_CLASS = "[.,   '’]"
const GROUPED_DIGITS = new RegExp(`^\\d{1,3}(?:${SEPARATOR_CLASS}\\d{3})+$`)
const SEPARATORS = new RegExp(SEPARATOR_CLASS, 'g')
const NUMBER_CHARS = new RegExp(`^(?:\\d|${SEPARATOR_CLASS})*$`)
const DIGITS = /^\d+$/
/** m:ss or h:mm:ss, optional 1-3 digit fraction after '.' or ','. */
const TIME = /^(\d+):([0-5]\d)(?::([0-5]\d))?(?:[.,](\d{1,3}))?$/

const TIME_SEPARATOR = ':'
const SECONDS_PER_MINUTE = 60
const SECONDS_PER_HOUR = 3600
const MAX_DECIMAL_DIGITS = 2

export default function parseScore(value) {
  if (value == null) return NaN
  const text = String(value).trim()
  if (text === '') return NaN

  if (text.includes(TIME_SEPARATOR)) return parseTime(text)

  const { sign, unsigned } = splitSign(text)
  if (unsigned === '' || !NUMBER_CHARS.test(unsigned)) return NaN

  const { integer, fraction } = splitDecimalPoint(unsigned)
  const digits = integerDigits(integer)
  return digits === null ? NaN : toFiniteNumber(sign, digits, fraction)
}

/** Total seconds of an m:ss or h:mm:ss time, keeping its fraction. */
function parseTime(text) {
  const match = TIME.exec(text)
  if (!match) return NaN
  const [, first, second, third, fraction] = match
  const hasHours = third !== undefined
  const totalSeconds = hasHours
    ? Number(first) * SECONDS_PER_HOUR + Number(second) * SECONDS_PER_MINUTE + Number(third)
    : Number(first) * SECONDS_PER_MINUTE + Number(second)
  return toFiniteNumber('', String(totalSeconds), fraction)
}

function splitSign(text) {
  const first = text[0]
  if (first !== '+' && first !== '-') return { sign: '', unsigned: text }
  return { sign: first === '-' ? '-' : '', unsigned: text.slice(1) }
}

function wholeNumber(text) {
  return { integer: text, fraction: '' }
}

/**
 * The last '.' or ',' is a decimal point when at most MAX_DECIMAL_DIGITS
 * digits follow it or nothing but '0' precedes it ("0.125", ".5");
 * otherwise it is a grouping separator ("40.456").
 */
function splitDecimalPoint(text) {
  const pointIndex = Math.max(text.lastIndexOf('.'), text.lastIndexOf(','))
  if (pointIndex === -1) return wholeNumber(text)
  const before = text.slice(0, pointIndex)
  const after = text.slice(pointIndex + 1)
  const hasNoIntegerPart = before === '0' || before === ''
  const isDecimalPoint = DIGITS.test(after) && (after.length <= MAX_DECIMAL_DIGITS || hasNoIntegerPart)
  if (!isDecimalPoint) return wholeNumber(text)
  return { integer: before === '' ? '0' : before, fraction: after }
}

/** Plain digits, or digits in groups of three, without the separators; null otherwise. */
function integerDigits(integer) {
  if (DIGITS.test(integer)) return integer
  if (GROUPED_DIGITS.test(integer)) return integer.replace(SEPARATORS, '')
  return null
}

function toFiniteNumber(sign, digits, fraction) {
  const number = Number(sign + digits + (fraction ? '.' + fraction : ''))
  if (!Number.isFinite(number)) return NaN
  return withoutNegativeZero(number)
}

function withoutNegativeZero(number) {
  return number === 0 ? 0 : number
}

/**
 * True when the trimmed score is filled in but is not a number under the spec
 * (it is still accepted, but may not rank correctly). A blank score is missing,
 * not non-numeric.
 */
export function isNonNumericScore(score) {
  const text = String(score ?? '').trim()
  return text !== '' && isNaN(parseScore(text))
}
