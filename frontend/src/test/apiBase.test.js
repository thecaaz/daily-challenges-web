import { describe, it, expect } from 'vitest'
import { resolveBackendBase } from '../api'

describe('resolveBackendBase', () => {
  it('uses a configured URL without trailing slash', () => {
    expect(resolveBackendBase('https://api.example.com/', false)).toBe('https://api.example.com')
  })

  it('treats an empty value as same origin', () => {
    expect(resolveBackendBase('', false)).toBe('')
    expect(resolveBackendBase('  ', true)).toBe('')
  })

  it('falls back to the local backend only in dev when unset', () => {
    expect(resolveBackendBase(undefined, true)).toBe('http://localhost:5000')
    expect(resolveBackendBase(undefined, false)).toBe('')
  })
})
