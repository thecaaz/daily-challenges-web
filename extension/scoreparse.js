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
  // m:ss or h:mm:ss, optional 1-3 digit fraction after '.' or ','.
  var TIME = /^(\d+):([0-5]\d)(?::([0-5]\d))?(?:[.,](\d{1,3}))?$/

  // First thing in free text that looks like a score: digits joined by
  // grouping/decimal separators or ':' (times), with an optional sign.
  var TOKEN = new RegExp('[+-]?\\d+(?:(?:' + SEP_CLASS + '|:)\\d+)*')

  function finite(n) {
    if (!Number.isFinite(n)) return null
    return n === 0 ? 0 : n // no -0
  }

  function parseTime(s) {
    var m = TIME.exec(s)
    if (!m) return null
    var total
    if (m[3] !== undefined) {
      total = Number(m[1]) * 3600 + Number(m[2]) * 60 + Number(m[3])
    } else {
      total = Number(m[1]) * 60 + Number(m[2])
    }
    return finite(Number(String(total) + (m[4] ? '.' + m[4] : '')))
  }

  /**
   * Parse a whole score string. Returns a number, or null when the trimmed
   * string is not exactly one score.
   *
   * opts.integerOnly: never treat a '.'/',' as a decimal point (for games
   * whose scores are always whole numbers, e.g. TimeGuessr "40.456").
   */
  function parseScore(input, opts) {
    if (input == null) return null
    var s = String(input).trim()
    if (s === '') return null
    var integerOnly = !!(opts && opts.integerOnly)

    if (s.indexOf(':') !== -1) return integerOnly ? null : parseTime(s)

    var sign = ''
    if (s[0] === '+' || s[0] === '-') {
      sign = s[0] === '-' ? '-' : ''
      s = s.slice(1)
    }
    if (s === '' || !NUMBER_CHARS.test(s)) return null

    var intPart = s
    var frac = ''
    if (!integerOnly) {
      var idx = Math.max(s.lastIndexOf('.'), s.lastIndexOf(','))
      if (idx !== -1) {
        var before = s.slice(0, idx)
        var after = s.slice(idx + 1)
        if (/^\d+$/.test(after) && (after.length <= 2 || before === '0' || before === '')) {
          intPart = before === '' ? '0' : before
          frac = after
        }
      }
    }

    var digits
    if (/^\d+$/.test(intPart)) digits = intPart
    else if (GROUPED.test(intPart)) digits = intPart.replace(SEPARATORS, '')
    else return null

    return finite(Number(sign + digits + (frac ? '.' + frac : '')))
  }

  /** Find the first score-looking token in free page text, or null. */
  function findScoreToken(text) {
    if (text == null) return null
    var m = String(text).match(TOKEN)
    return m ? m[0] : null
  }

  /** Read the first score in free page text and parse it with the spec. */
  function readNumber(text, opts) {
    var token = findScoreToken(text)
    return token === null ? null : parseScore(token, opts)
  }

  return { parseScore: parseScore, findScoreToken: findScoreToken, readNumber: readNumber }
})
