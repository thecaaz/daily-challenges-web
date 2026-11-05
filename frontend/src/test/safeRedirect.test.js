import { describe, it, expect } from 'vitest'
import { getSafeRedirect, isSafeRedirect, withRedirect } from '../utils/safeRedirect'

describe('getSafeRedirect', () => {
  it('accepts in-app paths', () => {
    expect(getSafeRedirect('/leagues/join/x')).toBe('/leagues/join/x')
    expect(getSafeRedirect('/games/3?scoringDay=2026-01-01')).toBe('/games/3?scoringDay=2026-01-01')
  })

  it('rejects protocol-relative URLs', () => {
    expect(getSafeRedirect('//evil.com')).toBe('/')
    expect(isSafeRedirect('//evil.com')).toBe(false)
  })

  it('rejects absolute URLs', () => {
    expect(getSafeRedirect('https://evil.com')).toBe('/')
    expect(getSafeRedirect('javascript:alert(1)')).toBe('/')
  })

  it('rejects backslash and control-character tricks', () => {
    expect(getSafeRedirect('/\\evil.com')).toBe('/')
    expect(getSafeRedirect('/\t/evil.com')).toBe('/')
  })

  it('returns the fallback for missing values', () => {
    expect(getSafeRedirect(null, '/home')).toBe('/home')
    expect(getSafeRedirect('', '/home')).toBe('/home')
  })
})

describe('withRedirect', () => {
  it('encodes the redirect target', () => {
    expect(withRedirect('/login', '/leagues/join/a b')).toBe('/login?redirect=%2Fleagues%2Fjoin%2Fa%20b')
  })

  it('drops unsafe or root redirects', () => {
    expect(withRedirect('/login', '//evil.com')).toBe('/login')
    expect(withRedirect('/login', '/')).toBe('/login')
  })
})
