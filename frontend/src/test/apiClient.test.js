import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import api, { authApi, setAccessToken, getApiRoot, AUTH_EXPIRED_EVENT } from '../api'

const ok = (config, data = {}) => ({ data, status: 200, headers: {}, config })

const httpError = (config, status) => {
  const err = new Error(`Request failed with status code ${status}`)
  err.config = config
  err.response = { status, data: {}, config, headers: {} }
  err.isAxiosError = true
  return err
}

const deferred = () => {
  let resolve
  let reject
  const promise = new Promise((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

describe('api.js request handling', () => {
  let apiAdapter
  let refreshAdapter

  beforeEach(() => {
    apiAdapter = vi.fn()
    refreshAdapter = vi.fn()
    api.defaults.adapter = apiAdapter
    authApi.defaults.adapter = refreshAdapter
  })

  afterEach(() => {
    setAccessToken(null)
  })

  describe('GET dedupe', () => {
    it('shares one request between identical concurrent GETs', async () => {
      apiAdapter.mockImplementation(async config => ok(config, { n: 1 }))

      const [a, b] = await Promise.all([api.get('/things'), api.get('/things')])

      expect(apiAdapter).toHaveBeenCalledTimes(1)
      expect(a).toBe(b)
    })

    it('sends separate requests for different params', async () => {
      apiAdapter.mockImplementation(async config => ok(config))

      await Promise.all([api.get('/things', { params: { page: 1 } }), api.get('/things', { params: { page: 2 } })])

      expect(apiAdapter).toHaveBeenCalledTimes(2)
    })

    it('does not dedupe when the caller opts out', async () => {
      apiAdapter.mockImplementation(async config => ok(config))

      await Promise.all([api.get('/things', { dedupe: false }), api.get('/things', { dedupe: false })])

      expect(apiAdapter).toHaveBeenCalledTimes(2)
    })

    it('sends a new request once the previous one settled', async () => {
      apiAdapter.mockImplementation(async config => ok(config))

      await api.get('/things')
      await api.get('/things')

      expect(apiAdapter).toHaveBeenCalledTimes(2)
    })

    it('forgets a failed request so it can be retried', async () => {
      apiAdapter
        .mockImplementationOnce(async config => { throw httpError(config, 500) })
        .mockImplementationOnce(async config => ok(config, { n: 2 }))

      await expect(api.get('/things')).rejects.toThrow()
      const res = await api.get('/things')

      expect(res.data.n).toBe(2)
    })
  })

  it('attaches the in-memory access token', async () => {
    setAccessToken('abc')
    apiAdapter.mockImplementation(async config => ok(config, { auth: config.headers.Authorization }))

    const res = await api.post('/things')

    expect(res.data.auth).toBe('Bearer abc')
  })

  it('sends no Authorization header without a token', async () => {
    apiAdapter.mockImplementation(async config => ok(config, { auth: config.headers.Authorization }))

    const res = await api.post('/things')

    expect(res.data.auth).toBeUndefined()
  })

  it('getApiRoot strips the /api suffix', () => {
    expect(getApiRoot()).toBe(api.defaults.baseURL.replace(/\/api$/, ''))
  })

  describe('concurrent 401s', () => {
    it('refreshes once and retries every queued request with the new token', async () => {
      setAccessToken('old')
      const refresh = deferred()
      refreshAdapter.mockImplementationOnce(() => refresh.promise)
      apiAdapter.mockImplementation(async config => {
        if (config.headers.Authorization === 'Bearer new') return ok(config, { url: config.url })
        throw httpError(config, 401)
      })

      const first = api.post('/a')
      const second = api.post('/b')
      await vi.waitFor(() => expect(apiAdapter).toHaveBeenCalledTimes(2))
      refresh.resolve(ok({}, { accessToken: 'new' }))

      const [a, b] = await Promise.all([first, second])
      expect(a.data.url).toBe('/a')
      expect(b.data.url).toBe('/b')
      expect(refreshAdapter).toHaveBeenCalledTimes(1)
    })

    it('rejects every queued request with its own 401 when the refresh fails', async () => {
      setAccessToken('old')
      const expired = vi.fn()
      window.addEventListener(AUTH_EXPIRED_EVENT, expired)
      const refresh = deferred()
      refreshAdapter.mockImplementationOnce(() => refresh.promise)
      apiAdapter.mockImplementation(async config => { throw httpError(config, 401) })

      const first = api.post('/a')
      const second = api.post('/b')
      await vi.waitFor(() => expect(apiAdapter).toHaveBeenCalledTimes(2))
      refresh.reject(httpError({}, 500))

      await expect(first).rejects.toMatchObject({ config: expect.objectContaining({ url: '/a' }) })
      await expect(second).rejects.toMatchObject({ config: expect.objectContaining({ url: '/b' }) })
      expect(expired).toHaveBeenCalledTimes(1)
      window.removeEventListener(AUTH_EXPIRED_EVENT, expired)
    })

    it('does not refresh a second time for a retried request that fails again', async () => {
      setAccessToken('old')
      refreshAdapter.mockImplementation(async config => ok(config, { accessToken: 'new' }))
      apiAdapter.mockImplementation(async config => { throw httpError(config, 401) })

      await expect(api.post('/a')).rejects.toMatchObject({ response: { status: 401 } })

      expect(refreshAdapter).toHaveBeenCalledTimes(1)
      expect(apiAdapter).toHaveBeenCalledTimes(2)
    })
  })
})
