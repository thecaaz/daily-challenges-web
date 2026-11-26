import { describe, it, expect } from 'vitest'
import parseUtcDate from '../utils/parseUtcDate'

describe('parseUtcDate', () => {
  it('returns an invalid date for empty input', () => {
    expect(Number.isNaN(parseUtcDate('').getTime())).toBe(true)
    expect(Number.isNaN(parseUtcDate(null).getTime())).toBe(true)
  })

  it('returns a Date unchanged', () => {
    const date = new Date()
    expect(parseUtcDate(date)).toBe(date)
  })

  it('trusts an explicit timezone', () => {
    expect(parseUtcDate('2024-03-01T10:00:00+02:00').toISOString()).toBe('2024-03-01T08:00:00.000Z')
    expect(parseUtcDate('2024-03-01T10:00:00Z').toISOString()).toBe('2024-03-01T10:00:00.000Z')
  })

  it('reads a date-only value as midnight UTC', () => {
    expect(parseUtcDate('2024-03-01').toISOString()).toBe('2024-03-01T00:00:00.000Z')
  })

  it('reads an ISO timestamp without timezone as UTC', () => {
    expect(parseUtcDate(' 2024-03-01T10:20:30.5 ').toISOString()).toBe('2024-03-01T10:20:30.500Z')
  })

  it('falls back to Date.parse for other formats', () => {
    expect(parseUtcDate('March 1, 2024 10:00 GMT').toISOString()).toBe('2024-03-01T10:00:00.000Z')
  })

  it('returns an invalid date for garbage', () => {
    expect(Number.isNaN(parseUtcDate('not a date').getTime())).toBe(true)
  })
})
