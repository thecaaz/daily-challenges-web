import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import api, { authApi, setAccessToken, refreshSession, AUTH_EXPIRED_EVENT, REFRESH_LOCK_NAME } from '../api'

// Build an axios-style rejection for the given config/status.
const httpError = (config, status, data = {}) => {
  const err = new Error(`Request failed with status code ${status}`)
  err.config = config
  err.response = { status, data, config, headers: {} }
  err.isAxiosError = true
  return err
}

describe('api.js 401 handling', () => {
  let apiAdapter
  let refreshAdapter
  let expired

  beforeEach(() => {
    setAccessToken('old-token')
    apiAdapter = vi.fn()
    refreshAdapter = vi.fn()
    api.defaults.adapter = apiAdapter
    authApi.defaults.adapter = refreshAdapter
    expired = vi.fn()
    window.addEventListener(AUTH_EXPIRED_EVENT, expired)
  })

  afterEach(() => {
    window.removeEventListener(AUTH_EXPIRED_EVENT, expired)
    setAccessToken(null)
  })

  it('refreshes and retries with the new token', async () => {
    apiAdapter
      .mockImplementationOnce(async (config) => { throw httpError(config, 401) })
      .mockImplementationOnce(async (config) => ({ data: { ok: config.headers.Authorization }, status: 200, headers: {}, config }))
    refreshAdapter.mockImplementationOnce(async (config) => ({ data: { accessToken: 'new-token' }, status: 200, headers: {}, config }))

    const res = await api.post('/things')
    expect(res.data.ok).toBe('Bearer new-token')
    expect(expired).not.toHaveBeenCalled()
  })

  it('on refresh failure clears the token, fires auth:expired and rejects with the original error', async () => {
    apiAdapter.mockImplementation(async (config) => { throw httpError(config, 401, { message: 'original' }) })
    refreshAdapter.mockImplementation(async (config) => { throw httpError(config, 401) })

    await expect(api.post('/things')).rejects.toMatchObject({ response: { data: { message: 'original' } } })
    expect(expired).toHaveBeenCalledTimes(1)
    // A refresh 401 is retried once before the session counts as expired.
    expect(refreshAdapter).toHaveBeenCalledTimes(2)

    // The token was cleared: the next request goes out without Authorization.
    apiAdapter.mockImplementationOnce(async (config) => ({ data: config.headers.Authorization ?? null, status: 200, headers: {}, config }))
    const res = await api.post('/anon')
    expect(res.data).toBeNull()
  })

  it('treats a refresh response without a token as a failure', async () => {
    apiAdapter.mockImplementation(async (config) => { throw httpError(config, 401) })
    refreshAdapter.mockImplementation(async (config) => ({ data: {}, status: 200, headers: {}, config }))

    await expect(api.post('/things')).rejects.toMatchObject({ response: { status: 401 } })
    expect(expired).toHaveBeenCalledTimes(1)
    expect(apiAdapter).toHaveBeenCalledTimes(1)
  })

  it('does not try to refresh on a failed login', async () => {
    apiAdapter.mockImplementation(async (config) => { throw httpError(config, 401, { message: 'Invalid username or password' }) })

    await expect(api.post('/auth/login', {})).rejects.toMatchObject({ response: { data: { message: 'Invalid username or password' } } })
    expect(refreshAdapter).not.toHaveBeenCalled()
    expect(expired).not.toHaveBeenCalled()
  })

  it('retries a refresh that got 401 once (another tab may have rotated the cookie)', async () => {
    apiAdapter
      .mockImplementationOnce(async (config) => { throw httpError(config, 401) })
      .mockImplementationOnce(async (config) => ({ data: { ok: config.headers.Authorization }, status: 200, headers: {}, config }))
    refreshAdapter
      .mockImplementationOnce(async (config) => { throw httpError(config, 401) })
      .mockImplementationOnce(async (config) => ({ data: { accessToken: 'rotated-token' }, status: 200, headers: {}, config }))

    const res = await api.post('/things')
    expect(res.data.ok).toBe('Bearer rotated-token')
    expect(refreshAdapter).toHaveBeenCalledTimes(2)
    expect(expired).not.toHaveBeenCalled()
  })

  it('does not retry a refresh that failed with something other than 401', async () => {
    refreshAdapter.mockImplementation(async (config) => { throw httpError(config, 500) })
    await expect(refreshSession()).rejects.toMatchObject({ response: { status: 500 } })
    expect(refreshAdapter).toHaveBeenCalledTimes(1)
  })

  it('coalesces concurrent refreshes in this tab onto one request', async () => {
    refreshAdapter.mockImplementation(async (config) => ({ data: { accessToken: 'one' }, status: 200, headers: {}, config }))
    const [a, b] = await Promise.all([refreshSession(), refreshSession()])
    expect(a).toBe('one')
    expect(b).toBe('one')
    expect(refreshAdapter).toHaveBeenCalledTimes(1)
  })

  describe('with the Web Locks API', () => {
    let release
    let lockRequest

    beforeEach(() => {
      // A lock held by "another tab" until release() is called.
      let held = new Promise(r => { release = r })
      lockRequest = vi.fn(async (name, cb) => {
        await held
        held = Promise.resolve()
        return cb()
      })
      Object.defineProperty(navigator, 'locks', { value: { request: lockRequest }, configurable: true })
    })

    afterEach(() => {
      delete navigator.locks
    })

    it('refreshes only while holding the cross-tab lock', async () => {
      refreshAdapter.mockImplementation(async (config) => ({ data: { accessToken: 'locked' }, status: 200, headers: {}, config }))

      const p = refreshSession()
      await new Promise(r => setTimeout(r, 10))
      expect(lockRequest).toHaveBeenCalledWith(REFRESH_LOCK_NAME, expect.any(Function))
      // Another tab still holds the lock: no request yet.
      expect(refreshAdapter).not.toHaveBeenCalled()

      release()
      await expect(p).resolves.toBe('locked')
      expect(refreshAdapter).toHaveBeenCalledTimes(1)
    })

    it('still coalesces callers in this tab onto one lock request', async () => {
      refreshAdapter.mockImplementation(async (config) => ({ data: { accessToken: 'locked' }, status: 200, headers: {}, config }))
      release()
      await Promise.all([refreshSession(), refreshSession(), refreshSession()])
      expect(lockRequest).toHaveBeenCalledTimes(1)
      expect(refreshAdapter).toHaveBeenCalledTimes(1)
    })
  })
})
