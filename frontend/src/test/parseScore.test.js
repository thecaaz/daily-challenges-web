import { describe, it, expect } from 'vitest'
import parseScore from '../utils/parseScore'

// These cases mirror tests/backend/ScoreParserTests.cs. The two parsers must
// agree, because SubmissionForm normalises with parseScore before POSTing and
// the server re-parses the result with ScoreParser.

describe('parseScore', () => {
  it('parses plain integers', () => {
    expect(parseScore('42')).toBe(42)
    expect(parseScore('-7')).toBe(-7)
    expect(parseScore('0')).toBe(0)
  })

  it('parses decimals with either separator', () => {
    expect(parseScore('3.14')).toBeCloseTo(3.14, 5)
    expect(parseScore('3,14')).toBeCloseTo(3.14, 5)
    expect(parseScore('-0.5')).toBeCloseTo(-0.5, 5)
  })

  // Regression: a TimeGuessr score of 40456 renders as "40,456" on en-US and
  // "40.456" on de-DE. Treating either as a decimal sinks the submission to
  // the bottom of the leaderboard.
  it('treats a separator with three trailing digits as grouping', () => {
    expect(parseScore('40,456')).toBe(40456)
    expect(parseScore('40.456')).toBe(40456)
    expect(parseScore('1,000')).toBe(1000)
    expect(parseScore('1.000')).toBe(1000)
    expect(parseScore('50,000')).toBe(50000)
    expect(parseScore('1.000.000')).toBe(1000000)
    expect(parseScore('1,000,000')).toBe(1000000)
    expect(parseScore('-40.456')).toBe(-40456)
  })

  it('uses the last separator as the decimal point when mixed', () => {
    expect(parseScore('1.234,56')).toBeCloseTo(1234.56, 5)
    expect(parseScore('1,234,567.89')).toBeCloseTo(1234567.89, 5)
    expect(parseScore('1 234 567,89')).toBeCloseTo(1234567.89, 5)
  })

  it('returns NaN for non-numeric input', () => {
    expect(parseScore('')).toBeNaN()
    expect(parseScore(null)).toBeNaN()
    expect(parseScore(undefined)).toBeNaN()
    expect(parseScore('abc')).toBeNaN()
  })
})
