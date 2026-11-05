import { describe, it, expect } from 'vitest'
import { isSafeHttpUrl, safeHttpUrl } from '../utils/safeUrl'

describe('isSafeHttpUrl', () => {
  it.each([
    'https://example.com',
    'http://example.com/path?q=1#x',
    '  https://timeguessr.com/  ',
    'HTTPS://EXAMPLE.COM',
  ])('accepts %s', (url) => {
    expect(isSafeHttpUrl(url)).toBe(true)
  })

  it.each([
    'javascript:alert(1)',
    'JaVaScRiPt:alert(1)',
    ' javascript:alert(1)',
    'data:text/html,<script>alert(1)</script>',
    'vbscript:msgbox(1)',
    'file:///etc/passwd',
    'ftp://example.com',
    '//example.com',
    '/relative/path',
    'example.com',
    'http://',
    '',
    '   ',
    null,
    undefined,
    42,
    {},
  ])('rejects %s', (url) => {
    expect(isSafeHttpUrl(url)).toBe(false)
  })
})

describe('safeHttpUrl', () => {
  it('returns the trimmed URL when safe', () => {
    expect(safeHttpUrl(' https://a.example/ ')).toBe('https://a.example/')
  })
  it('returns null when unsafe', () => {
    expect(safeHttpUrl('javascript:void(0)')).toBeNull()
  })
})
