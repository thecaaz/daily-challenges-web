import axios from 'axios'

// Where the backend lives:
// - window.__BACKEND_URL set to a URL (from /env.js) -> that origin
// - window.__BACKEND_URL empty -> same origin; nginx proxies /api to the backend
// - window.__BACKEND_URL undefined in the Vite dev server (no /env.js) -> the
//   local backend on http://localhost:5000
// - undefined in a production build -> same origin
export function resolveBackendBase(configured, isDev = false) {
  if (typeof configured === 'string') {
    const trimmed = configured.trim()
    return trimmed ? trimmed.replace(/\/+$/, '') : ''
  }
  return isDev ? 'http://localhost:5000' : ''
}

const backendBase = resolveBackendBase(
  typeof window !== 'undefined' ? window.__BACKEND_URL : undefined,
  !!import.meta.env.DEV
)

const baseApiUrl = `${backendBase}/api`

// Fired on window when the session can't be refreshed; AuthContext clears the user.
export const AUTH_EXPIRED_EVENT = 'auth:expired'

// Primary axios instance used for API requests. It will attach an Authorization
// header from an in-memory access token when present, and will attempt a
// refresh flow on 401 responses.
const api = axios.create({
  baseURL: baseApiUrl,
  withCredentials: true,
})

// Dedicated instance for auth endpoints (refresh) that should not use the
// automatic interceptors to avoid infinite loops.
export const authApi = axios.create({ baseURL: baseApiUrl, withCredentials: true })

// In-flight GET dedupe: coalesce identical in-flight GET requests so they
// share a single network request. Useful in dev when React.StrictMode
// double-mounts components or when multiple components ask for the same
// resource simultaneously.
const _inflight = new Map()

const _buildKey = (method, url, config) => {
  const base = api.defaults.baseURL || ''
  const params = config && config.params ? JSON.stringify(config.params) : ''
  return `${method}:${base}${url}?params=${params}`
}

const _origGet = api.get.bind(api)

api.get = (url, config = {}) => {
  const dedupe = config.dedupe !== false
  if (!dedupe) return _origGet(url, config)

  const key = _buildKey('GET', url, config)
  if (_inflight.has(key)) return _inflight.get(key)

  const p = _origGet(url, config).finally(() => {
    _inflight.delete(key)
  })

  _inflight.set(key, p)
  return p
}

// In-memory access token (not persisted). Use `setAccessToken` to update it.
let _accessToken = null

export function setAccessToken(token) {
  _accessToken = token
}

export default api

// Origin the backend is served from, without the trailing /api.
// '' means same origin, so imageUrl() yields root-relative paths like '/uploads/x.png'.
export const getApiRoot = () => {
  const base = api.defaults.baseURL || baseApiUrl
  return base.replace(/\/api\/?$/, '')
}

// Attach Authorization header from in-memory token for every request
api.interceptors.request.use((config) => {
  if (_accessToken) {
    config.headers = config.headers || {}
    config.headers.Authorization = `Bearer ${_accessToken}`
  }
  return config
})

const extractToken = (data) =>
  data?.accessToken || data?.AccessToken || data?.user?.accessToken || null

// Name of the Web Lock that serializes refreshes across tabs of this origin.
export const REFRESH_LOCK_NAME = 'dc-auth-refresh'
// A refresh that got 401 is retried once after this delay before the session
// counts as expired (see refreshSession).
export const REFRESH_RETRY_DELAY_MS = 300

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms))

const postRefresh = async () => {
  const r = await authApi.post('/auth/refresh')
  const token = extractToken(r.data)
  if (!token) throw new Error('Refresh response had no access token')
  setAccessToken(token)
  return token
}

// The backend rotates the refresh cookie on every call and rejects a reused one
// with 401. If another tab refreshed between our cookie being read and sent,
// the cookie jar already holds the rotated cookie: try once more before giving up.
const refreshWithRetry = async () => {
  try {
    return await postRefresh()
  } catch (err) {
    if (err?.response?.status !== 401) throw err
    await sleep(REFRESH_RETRY_DELAY_MS)
    return postRefresh()
  }
}

// Run `fn` while holding the cross-tab refresh lock, so tabs refresh one after
// another (each with the cookie the previous one received). Without the Web
// Locks API, just run it (in-tab coalescing and the 401 retry still apply).
const withRefreshLock = (fn) => {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  if (locks && typeof locks.request === 'function') {
    return locks.request(REFRESH_LOCK_NAME, () => fn())
  }
  return fn()
}

// POST /auth/refresh, coalescing concurrent callers in this tab onto one
// request and serializing refreshes across tabs (Web Locks). The refresh cookie
// rotates on every call, so two parallel refreshes (React StrictMode double
// effects, app start racing a 401 retry, two tabs waking up together) would
// otherwise race each other and log one of them out. Resolves to the new
// access token (already stored); rejects if the refresh failed or returned no
// token.
let _refreshPromise = null
export function refreshSession() {
  if (!_refreshPromise) {
    _refreshPromise = Promise.resolve()
      .then(() => withRefreshLock(refreshWithRetry))
      .finally(() => { _refreshPromise = null })
  }
  return _refreshPromise
}

// Auth endpoints whose 401 means "bad credentials"/"no session", never "token expired".
const NO_REFRESH_URL = /\/auth\/(login|register|refresh|logout)(\b|\/|\?|$)/

const notifyAuthExpired = () => {
  if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') {
    window.dispatchEvent(new Event(AUTH_EXPIRED_EVENT))
  }
}

// 401 -> try refresh once, queue concurrent requests while refreshing
let isRefreshing = false
let failedQueue = []

const processQueue = (error, token = null) => {
  failedQueue.forEach(p => {
    if (error) p.reject(error)
    else p.resolve(token)
  })
  failedQueue = []
}

const retryWithToken = (request, token) => {
  request.headers = request.headers || {}
  request.headers.Authorization = `Bearer ${token}`
  return api.request(request)
}

api.interceptors.response.use(
  res => res,
  async (error) => {
    const originalRequest = error.config
    if (!originalRequest) return Promise.reject(error)

    const is401 = error.response && error.response.status === 401
    if (!is401 || originalRequest._retry || NO_REFRESH_URL.test(originalRequest.url || '')) {
      return Promise.reject(error)
    }

    originalRequest._retry = true

    if (isRefreshing) {
      // Wait for the refresh in flight; on failure surface this request's own 401.
      return new Promise((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      }).then(
        token => retryWithToken(originalRequest, token),
        () => Promise.reject(error)
      )
    }

    isRefreshing = true
    let newToken = null
    try {
      newToken = await refreshSession()
      processQueue(null, newToken)
    } catch (refreshError) {
      processQueue(refreshError, null)
      setAccessToken(null)
      notifyAuthExpired()
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }

    return retryWithToken(originalRequest, newToken)
  }
)
