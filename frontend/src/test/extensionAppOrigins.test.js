import { describe, it, expect, beforeAll } from 'vitest'
import loadExtensionScript from './loadExtensionScript'

let origins

beforeAll(() => {
  loadExtensionScript('app-origins.js')
  origins = globalThis.ScoreBridgeOrigins
})

describe('extension app-origins originOf', () => {
  it('returns the origin of a URL', () => {
    expect(origins.originOf('https://challenges.caaz.dev/games/1?x=1')).toBe('https://challenges.caaz.dev')
  })

  it('returns null for empty, non-string or unparsable input', () => {
    expect(origins.originOf('')).toBeNull()
    expect(origins.originOf(undefined)).toBeNull()
    expect(origins.originOf(42)).toBeNull()
    expect(origins.originOf('not a url')).toBeNull()
  })
})

describe('extension app-origins isAppOrigin', () => {
  it('accepts the production and test app origins exactly', () => {
    expect(origins.isAppOrigin('https://challenges.caaz.dev')).toBe(true)
    expect(origins.isAppOrigin('https://test.challenges.caaz.dev')).toBe(true)
  })

  it('accepts localhost on any numeric port, or without a port, in the dev build', () => {
    expect(origins.isAppOrigin('http://localhost')).toBe(true)
    expect(origins.isAppOrigin('http://localhost:5173')).toBe(true)
  })

  it('rejects look-alikes, other schemes and non-numeric ports', () => {
    expect(origins.isAppOrigin('http://challenges.caaz.dev')).toBe(false)
    expect(origins.isAppOrigin('https://challenges.caaz.dev.evil.com')).toBe(false)
    expect(origins.isAppOrigin('https://localhost:5173')).toBe(false)
    expect(origins.isAppOrigin('http://localhost:abc')).toBe(false)
    expect(origins.isAppOrigin('http://localhost.evil.com')).toBe(false)
  })

  it('rejects empty, opaque and non-string origins', () => {
    expect(origins.isAppOrigin('')).toBe(false)
    expect(origins.isAppOrigin('null')).toBe(false)
    expect(origins.isAppOrigin(null)).toBe(false)
  })

  it('exposes a frozen copy of the allowlist', () => {
    expect(origins.APP_ORIGINS).toEqual(['https://challenges.caaz.dev', 'https://test.challenges.caaz.dev', 'http://localhost:*'])
    expect(Object.isFrozen(origins.APP_ORIGINS)).toBe(true)
    expect(Object.isFrozen(origins)).toBe(true)
  })
})
