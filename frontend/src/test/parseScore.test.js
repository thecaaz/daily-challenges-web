import { describe, it, expect } from 'vitest'
import parseScore from '../utils/parseScore'
// Shared spec, also used by the backend ScoreParserTests and the extension's
// scoreparse tests. Only tests may import from outside frontend/.
import spec from '../../../tests/fixtures/score-parsing.json'

describe('parseScore (shared fixture)', () => {
  it('has cases', () => {
    expect(spec.cases.length).toBeGreaterThan(10)
  })

  it.each(spec.cases.map(c => [JSON.stringify(c.input), c.input, c.expected]))('%s', (_label, input, expected) => {
    const result = parseScore(input)
    if (expected === null) {
      expect(result).toBeNaN()
    } else {
      expect(result).toBeCloseTo(expected, 9)
    }
  })
})

describe('parseScore (frontend specifics)', () => {
  it('returns NaN for null/undefined', () => {
    expect(parseScore(null)).toBeNaN()
    expect(parseScore(undefined)).toBeNaN()
  })

  // Regression: TimeGuessr 40456 renders as "40,456" (en-US) or "40.456" (de-DE).
  it('treats a separator with three trailing digits as grouping', () => {
    expect(parseScore('40,456')).toBe(40456)
    expect(parseScore('40.456')).toBe(40456)
    expect(parseScore('-40.456')).toBe(-40456)
  })

  it('never returns -0', () => {
    expect(Object.is(parseScore('-0'), 0)).toBe(true)
  })
})
