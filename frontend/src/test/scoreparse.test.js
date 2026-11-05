import { describe, it, expect } from 'vitest'
import { createRequire } from 'node:module'
import fixture from '../../../tests/fixtures/score-parsing.json'

// extension/scoreparse.js is a classic script with a UMD wrapper, so it is
// loaded the same way the content script gets it: as plain CommonJS.
const require = createRequire(import.meta.url)
const ScoreParse = require('../../../extension/scoreparse.js')

describe('extension scoreparse (shared spec)', () => {
  it('exposes the API on module.exports and globalThis', () => {
    expect(typeof ScoreParse.parseScore).toBe('function')
    expect(globalThis.ScoreParse).toBe(ScoreParse)
  })

  for (const { input, expected } of fixture.cases) {
    it(`parses ${JSON.stringify(input)} -> ${expected}`, () => {
      const actual = ScoreParse.parseScore(input)
      if (expected === null) expect(actual).toBeNull()
      else expect(actual).toBeCloseTo(expected, 9)
    })
  }

  it('returns null for null/undefined', () => {
    expect(ScoreParse.parseScore(null)).toBeNull()
    expect(ScoreParse.parseScore(undefined)).toBeNull()
  })
})

describe('integerOnly', () => {
  const { parseScore } = ScoreParse
  it('treats every separator as grouping', () => {
    expect(parseScore('40.456', { integerOnly: true })).toBe(40456)
    expect(parseScore('40,456', { integerOnly: true })).toBe(40456)
    expect(parseScore('0.125', { integerOnly: true })).toBe(125)
  })
  it('rejects values that are not whole numbers', () => {
    expect(parseScore('40.45', { integerOnly: true })).toBeNull()
    expect(parseScore('2:30', { integerOnly: true })).toBeNull()
  })
})

describe('readNumber (free page text)', () => {
  const { readNumber, findScoreToken } = ScoreParse
  it('extracts the first score token from text', () => {
    expect(findScoreToken('Total: 40,456 / 50,000')).toBe('40,456')
    expect(readNumber('Total: 40,456 / 50,000')).toBe(40456)
    expect(readNumber('Final score 437.25 out of 500')).toBe(437.25)
    expect(readNumber('Time 2:05')).toBe(125)
    expect(readNumber("40'456", { integerOnly: true })).toBe(40456)
  })
  it('returns null when there is no number', () => {
    expect(readNumber('no score yet')).toBeNull()
    expect(readNumber(null)).toBeNull()
  })
})
