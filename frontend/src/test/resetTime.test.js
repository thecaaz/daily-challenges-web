import { describe, it, expect } from 'vitest'
import { utcToLocalHHmm, localToUtcHHmm } from '../utils/resetTime'

const UTC_OFFSET = 0
const UTC_PLUS_1_OFFSET = -60
const UTC_MINUS_5_OFFSET = 300
const UTC_PLUS_5_30_OFFSET = -330
const UTC_MINUS_12_OFFSET = 720
const UTC_PLUS_14_OFFSET = -840

const MINUTES_PER_HOUR = 60
const MINUTES_PER_DAY = 24 * MINUTES_PER_HOUR
const QUARTER_HOUR_MINUTES = 15

const toHHmm = minutesOfDay => {
  const hours = String(Math.floor(minutesOfDay / MINUTES_PER_HOUR)).padStart(2, '0')
  const minutes = String(minutesOfDay % MINUTES_PER_HOUR).padStart(2, '0')
  return `${hours}:${minutes}`
}

const everyQuarterHour = () =>
  Array.from({ length: MINUTES_PER_DAY / QUARTER_HOUR_MINUTES }, (_, index) => toHHmm(index * QUARTER_HOUR_MINUTES))

describe('resetTime conversion', () => {
  it('moves a time forward for a zone ahead of UTC', () => {
    expect(utcToLocalHHmm('05:00', UTC_PLUS_1_OFFSET)).toBe('06:00')
    expect(localToUtcHHmm('06:00', UTC_PLUS_1_OFFSET)).toBe('05:00')
  })

  it('moves a time back for a zone behind UTC', () => {
    expect(utcToLocalHHmm('12:30', UTC_MINUS_5_OFFSET)).toBe('07:30')
    expect(localToUtcHHmm('07:30', UTC_MINUS_5_OFFSET)).toBe('12:30')
  })

  it('wraps across midnight', () => {
    expect(utcToLocalHHmm('23:30', UTC_PLUS_1_OFFSET)).toBe('00:30')
    expect(localToUtcHHmm('00:30', UTC_PLUS_1_OFFSET)).toBe('23:30')
    expect(utcToLocalHHmm('02:00', UTC_MINUS_5_OFFSET)).toBe('21:00')
    expect(localToUtcHHmm('21:00', UTC_MINUS_5_OFFSET)).toBe('02:00')
    expect(utcToLocalHHmm('00:00', UTC_OFFSET)).toBe('00:00')
  })

  it('handles half-hour offsets', () => {
    expect(utcToLocalHHmm('20:00', UTC_PLUS_5_30_OFFSET)).toBe('01:30')
    expect(localToUtcHHmm('01:30', UTC_PLUS_5_30_OFFSET)).toBe('20:00')
  })

  it.each([UTC_PLUS_1_OFFSET, UTC_MINUS_5_OFFSET, UTC_PLUS_5_30_OFFSET, UTC_OFFSET, UTC_MINUS_12_OFFSET, UTC_PLUS_14_OFFSET])(
    'converts every quarter hour to local and back unchanged at offset %i',
    offset => {
      for (const hhmm of everyQuarterHour()) {
        expect(localToUtcHHmm(utcToLocalHHmm(hhmm, offset), offset)).toBe(hhmm)
      }
    }
  )

  it('accepts times with seconds', () => {
    expect(utcToLocalHHmm('05:00:00', UTC_PLUS_1_OFFSET)).toBe('06:00')
  })

  it('returns null for empty, out-of-range or missing times', () => {
    expect(utcToLocalHHmm('', UTC_PLUS_1_OFFSET)).toBeNull()
    expect(utcToLocalHHmm('24:00', UTC_PLUS_1_OFFSET)).toBeNull()
    expect(localToUtcHHmm(null, UTC_PLUS_1_OFFSET)).toBeNull()
  })
})
