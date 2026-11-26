import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import api, { authApi, setAccessToken, refreshSession, AUTH_EXPIRED_EVENT, REFRESH_LOCK_NAME } from '../api'

function httpError(config, status, data = {}) {
  const err = new Error(`Request failed with status code ${status}`)
  err.config = config
  err.response = { status, data, config, headers: {} }
  err.isAxiosError = true
  return err
}

const failWith = (status, data) => async config => { throw httpError(config, status, data) }
const respondWith = data => async config => ({ data, status: 200, headers: {}, config })
const echoAuthorization = async config => ({ data: { ok: config.headers.Authorization }, status: 200, headers: {}, config })
const newAccessToken = token => respondWith({ accessToken: token })

function lockHeldByAnotherTab() {
  let release
  let held = new Promise(resolve => { release = resolve })
  const request = vi.fn(async (name, callback) => {
    await held
    held = Promise.resolve()
    return callback()
  })
  return { request, release: () => release() }
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
      .mockImplementationOnce(failWith(401))
      .mockImplementationOnce(echoAuthorization)
    refreshAdapter.mockImplementationOnce(newAccessToken('new-token'))

    const res = await api.post('/things')
    expect(res.data.ok).toBe('Bearer new-token')
    expect(expired).not.toHaveBeenCalled()
  })

  it('on refresh failure fires auth:expired and rejects with the original error', async () => {
    apiAdapter.mockImplementation(failWith(401, { message: 'original' }))
    refreshAdapter.mockImplementation(failWith(401))

    await expect(api.post('/things')).rejects.toMatchObject({ response: { data: { message: 'original' } } })

    expect(expired).toHaveBeenCalledTimes(1)
  })

  it('retries a refresh 401 once before the session counts as expired', async () => {
    apiAdapter.mockImplementation(failWith(401))
    refreshAdapter.mockImplementation(failWith(401))

    await expect(api.post('/things')).rejects.toMatchObject({ response: { status: 401 } })

    expect(refreshAdapter).toHaveBeenCalledTimes(2)
  })

  it('clears the token on refresh failure so the next request goes out without Authorization', async () => {
    apiAdapter.mockImplementationOnce(failWith(401))
    refreshAdapter.mockImplementation(failWith(401))
    await expect(api.post('/things')).rejects.toBeDefined()

    apiAdapter.mockImplementationOnce(echoAuthorization)
    const res = await api.post('/anon')

    expect(res.data.ok).toBeUndefined()
  })

  it('treats a refresh response without a token as a failure', async () => {
    apiAdapter.mockImplementation(failWith(401))
    refreshAdapter.mockImplementation(respondWith({}))

    await expect(api.post('/things')).rejects.toMatchObject({ response: { status: 401 } })
    expect(expired).toHaveBeenCalledTimes(1)
    expect(apiAdapter).toHaveBeenCalledTimes(1)
  })

  it('does not try to refresh on a failed login', async () => {
    apiAdapter.mockImplementation(failWith(401, { message: 'Invalid username or password' }))

    await expect(api.post('/auth/login', {})).rejects.toMatchObject({ response: { data: { message: 'Invalid username or password' } } })
    expect(refreshAdapter).not.toHaveBeenCalled()
    expect(expired).not.toHaveBeenCalled()
  })

  it('retries a refresh that got 401 once (another tab may have rotated the cookie)', async () => {
    apiAdapter
      .mockImplementationOnce(failWith(401))
      .mockImplementationOnce(echoAuthorization)
    refreshAdapter
      .mockImplementationOnce(failWith(401))
      .mockImplementationOnce(newAccessToken('rotated-token'))

    const res = await api.post('/things')
    expect(res.data.ok).toBe('Bearer rotated-token')
    expect(refreshAdapter).toHaveBeenCalledTimes(2)
    expect(expired).not.toHaveBeenCalled()
  })

  it('does not retry a refresh that failed with something other than 401', async () => {
    refreshAdapter.mockImplementation(failWith(500))
    await expect(refreshSession()).rejects.toMatchObject({ response: { status: 500 } })
    expect(refreshAdapter).toHaveBeenCalledTimes(1)
  })

  it('coalesces concurrent refreshes in this tab onto one request', async () => {
    refreshAdapter.mockImplementation(newAccessToken('one'))
    const [a, b] = await Promise.all([refreshSession(), refreshSession()])
    expect(a).toBe('one')
    expect(b).toBe('one')
    expect(refreshAdapter).toHaveBeenCalledTimes(1)
  })

  describe('with the Web Locks API', () => {
    let release
    let lockRequest

    beforeEach(() => {
      const lock = lockHeldByAnotherTab()
      release = lock.release
      lockRequest = lock.request
      Object.defineProperty(navigator, 'locks', { value: { request: lockRequest }, configurable: true })
    })

    afterEach(() => {
      delete navigator.locks
    })

    it('refreshes only after the other tab releases the cross-tab lock', async () => {
      refreshAdapter.mockImplementation(newAccessToken('locked'))
      const pending = refreshSession()
      await new Promise(r => setTimeout(r, 10))
      expect(lockRequest).toHaveBeenCalledWith(REFRESH_LOCK_NAME, expect.any(Function))
      expect(refreshAdapter).not.toHaveBeenCalled()

      release()

      await expect(pending).resolves.toBe('locked')
      expect(refreshAdapter).toHaveBeenCalledTimes(1)
    })

    it('still coalesces callers in this tab onto one lock request', async () => {
      refreshAdapter.mockImplementation(newAccessToken('locked'))
      release()
      await Promise.all([refreshSession(), refreshSession(), refreshSession()])
      expect(lockRequest).toHaveBeenCalledTimes(1)
      expect(refreshAdapter).toHaveBeenCalledTimes(1)
    })
  })
})
