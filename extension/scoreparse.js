// ScoreBridge score parsing.
//
// Implements the shared spec in tests/fixtures/score-parsing.json, which the
// backend ScoreParser and the frontend parseScore also follow. Loaded as a
// classic content script before content-script.js (sets globalThis.ScoreParse)
// and required from vitest (module.exports).
;(function (root, factory) {
  'use strict'
  var api = factory()
  if (typeof module === 'object' && module && module.exports) module.exports = api
  if (root) root.ScoreParse = api
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict'

  // Grouping separators: '.', ',', space, NBSP, narrow NBSP, apostrophe, right single quote.
  var SEP_CLASS = "[., \u00A0\u202F'\u2019]"
  var GROUPED = new RegExp('^\\d{1,3}(?:' + SEP_CLASS + '\\d{3})+$')
  var SEPARATORS = new RegExp(SEP_CLASS, 'g')
  var NUMBER_CHARS = new RegExp('^(?:\\d|' + SEP_CLASS + ')*$')
  var DIGITS = /^\d+$/
  // m:ss or h:mm:ss, optional 1-3 digit fraction after '.' or ','.
  var TIME = /^(\d+):([0-5]\d)(?::([0-5]\d))?(?:[.,](\d{1,3}))?$/

  // First thing in free text that looks like a score: digits joined by
  // grouping/decimal separators or ':' (times), with an optional sign.
  var TOKEN = new RegExp('[+-]?\\d+(?:(?:' + SEP_CLASS + '|:)\\d+)*')

  var TIME_SEPARATOR = ':'
  var SECONDS_PER_MINUTE = 60
  var SECONDS_PER_HOUR = 3600
  var MAX_DECIMAL_DIGITS = 2

  /**
   * Parse a whole score string. Returns a number, or null when the trimmed
   * string is not exactly one score.
   *
   * opts.integerOnly: never treat a '.'/',' as a decimal point (for games
   * whose scores are always whole numbers, e.g. TimeGuessr "40.456").
   */
  function parseScore(input, opts) {
    if (input == null) return null
    var text = String(input).trim()
    if (text === '') return null
    var integerOnly = !!(opts && opts.integerOnly)

    if (text.indexOf(TIME_SEPARATOR) !== -1) return integerOnly ? null : parseTime(text)

    var signed = splitSign(text)
    if (signed.unsigned === '' || !NUMBER_CHARS.test(signed.unsigned)) return null

    var parts = integerOnly ? wholeNumber(signed.unsigned) : splitDecimalPoint(signed.unsigned)
    var digits = integerDigits(parts.integer)
    return digits === null ? null : toFiniteNumber(signed.sign, digits, parts.fraction)
  }

  /** Total seconds of an m:ss or h:mm:ss time, keeping its fraction. */
  function parseTime(text) {
    var match = TIME.exec(text)
    if (!match) return null
    var hasHours = match[3] !== undefined
    var totalSeconds = hasHours
      ? Number(match[1]) * SECONDS_PER_HOUR + Number(match[2]) * SECONDS_PER_MINUTE + Number(match[3])
      : Number(match[1]) * SECONDS_PER_MINUTE + Number(match[2])
    return toFiniteNumber('', String(totalSeconds), match[4])
  }

  function splitSign(text) {
    var first = text[0]
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
    var pointIndex = Math.max(text.lastIndexOf('.'), text.lastIndexOf(','))
    if (pointIndex === -1) return wholeNumber(text)
    var before = text.slice(0, pointIndex)
    var after = text.slice(pointIndex + 1)
    var hasNoIntegerPart = before === '0' || before === ''
    var isDecimalPoint = DIGITS.test(after) && (after.length <= MAX_DECIMAL_DIGITS || hasNoIntegerPart)
    if (!isDecimalPoint) return wholeNumber(text)
    return { integer: before === '' ? '0' : before, fraction: after }
  }

  /** Plain digits, or digits in groups of three, without the separators; null otherwise. */
  function integerDigits(integer) {
    if (DIGITS.test(integer)) return integer
    if (GROUPED.test(integer)) return integer.replace(SEPARATORS, '')
    return null
  }

  function toFiniteNumber(sign, digits, fraction) {
    var value = Number(sign + digits + (fraction ? '.' + fraction : ''))
    if (!Number.isFinite(value)) return null
    return withoutNegativeZero(value)
  }

  function withoutNegativeZero(value) {
    return value === 0 ? 0 : value
  }

  /** Find the first score-looking token in free page text, or null. */
  function findScoreToken(text) {
    if (text == null) return null
    var match = String(text).match(TOKEN)
    return match ? match[0] : null
  }

  /** Read the first score in free page text and parse it with the spec. */
  function readNumber(text, opts) {
    var token = findScoreToken(text)
    return token === null ? null : parseScore(token, opts)
  }

  return { parseScore: parseScore, findScoreToken: findScoreToken, readNumber: readNumber }
})
