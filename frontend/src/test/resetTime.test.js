import { describe, it, expect } from 'vitest'
import { utcToLocalHHmm, localToUtcHHmm } from '../utils/resetTime'

describe('resetTime conversion', () => {
  it('offset -60 (UTC+1)', () => {
    expect(utcToLocalHHmm('05:00', -60)).toBe('06:00')
    expect(localToUtcHHmm('06:00', -60)).toBe('05:00')
  })

  it('offset +300 (UTC-5)', () => {
    expect(utcToLocalHHmm('12:30', 300)).toBe('07:30')
    expect(localToUtcHHmm('07:30', 300)).toBe('12:30')
  })

  it('wraps across midnight', () => {
    expect(utcToLocalHHmm('23:30', -60)).toBe('00:30')
    expect(localToUtcHHmm('00:30', -60)).toBe('23:30')
    expect(utcToLocalHHmm('02:00', 300)).toBe('21:00')
    expect(localToUtcHHmm('21:00', 300)).toBe('02:00')
    expect(utcToLocalHHmm('00:00', 0)).toBe('00:00')
  })

  it('handles half-hour offsets', () => {
    expect(utcToLocalHHmm('20:00', -330)).toBe('01:30')
    expect(localToUtcHHmm('01:30', -330)).toBe('20:00')
  })

  it('round trips for every quarter hour', () => {
    for (const offset of [-60, 300, -330, 0, 720, -840]) {
      for (let t = 0; t < 24 * 60; t += 15) {
        const hhmm = `${String(Math.floor(t / 60)).padStart(2, '0')}:${String(t % 60).padStart(2, '0')}`
        expect(localToUtcHHmm(utcToLocalHHmm(hhmm, offset), offset)).toBe(hhmm)
      }
    }
  })

  it('accepts HH:mm:ss and rejects garbage', () => {
    expect(utcToLocalHHmm('05:00:00', -60)).toBe('06:00')
    expect(utcToLocalHHmm('', -60)).toBeNull()
    expect(utcToLocalHHmm('24:00', -60)).toBeNull()
    expect(localToUtcHHmm(null, -60)).toBeNull()
  })
})
