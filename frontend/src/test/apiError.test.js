import { describe, it, expect } from 'vitest'
import { getApiErrorMessage } from '../utils/apiError'

const resp = (status, data) => ({ response: { status, data } })

describe('getApiErrorMessage', () => {
  it('uses the backend { message } body', () => {
    expect(getApiErrorMessage(resp(409, { message: 'Already a member' }), 'fb')).toBe('Already a member')
  })

  it('uses the first ProblemDetails errors entry', () => {
    const data = { title: 'One or more validation errors occurred.', errors: { Name: ['The Name field is required.'] } }
    expect(getApiErrorMessage(resp(400, data), 'fb')).toBe('The Name field is required.')
  })

  it('falls back to the ProblemDetails title when there are no errors', () => {
    expect(getApiErrorMessage(resp(400, { title: 'Bad thing' }), 'fb')).toBe('Bad thing')
  })

  it('maps a 413 with an nginx HTML body to a friendly message', () => {
    const html = '<html><head><title>413 Request Entity Too Large</title></head><body><center><h1>413 Request Entity Too Large</h1></center><hr><center>nginx</center></body></html>'
    expect(getApiErrorMessage(resp(413, html), 'fb')).toBe('Screenshot too large')
  })

  it('maps 429 to a rate-limit message', () => {
    expect(getApiErrorMessage(resp(429, ''), 'fb')).toBe('Too many attempts, please wait a minute')
  })

  it('prefers a backend message over the status text', () => {
    expect(getApiErrorMessage(resp(429, { message: 'Slow down' }), 'fb')).toBe('Slow down')
  })

  it('returns the fallback on a network error', () => {
    const err = Object.assign(new Error('Network Error'), { code: 'ERR_NETWORK', request: {} })
    expect(getApiErrorMessage(err, 'Could not connect')).toBe('Could not connect')
  })

  it('never stringifies an object body', () => {
    const msg = getApiErrorMessage(resp(400, { foo: { bar: 1 } }), 'Failed to save')
    expect(msg).toBe('Failed to save')
    expect(msg).not.toContain('[object')
  })

  it('ignores a non-string message field', () => {
    expect(getApiErrorMessage(resp(400, { message: { nested: true } }), 'fb')).toBe('fb')
  })

  it('uses a generic text for 5xx without a usable body', () => {
    expect(getApiErrorMessage(resp(502, '<html>Bad Gateway</html>'), 'fb')).toBe('Server error, please try again later')
  })

  it('handles null/undefined errors', () => {
    expect(getApiErrorMessage(undefined, 'fb')).toBe('fb')
    expect(getApiErrorMessage(null)).toBe('Something went wrong')
  })
})
