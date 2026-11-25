import axios from 'axios'

const DEV_BACKEND_URL = 'http://localhost:5000'
const TRAILING_SLASHES = /\/+$/
const API_PATH_SUFFIX = /\/api\/?$/
const UNAUTHORIZED = 401

/**
 * Where the backend lives:
 * - window.__BACKEND_URL set to a URL (from /env.js) -> that origin
 * - window.__BACKEND_URL empty -> same origin; nginx proxies /api to the backend
 * - window.__BACKEND_URL undefined in the Vite dev server (no /env.js) -> the
 *   local backend on http://localhost:5000
 * - undefined in a production build -> same origin
 */
export function resolveBackendBase(configured, isDev = false) {
  if (typeof configured === 'string') {
    const trimmed = configured.trim()
    return trimmed ? trimmed.replace(TRAILING_SLASHES, '') : ''
  }
  return isDev ? DEV_BACKEND_URL : ''
}

const backendBase = resolveBackendBase(
  typeof window !== 'undefined' ? window.__BACKEND_URL : undefined,
  !!import.meta.env.DEV
)

const baseApiUrl = `${backendBase}/api`

/** Fired on window when the session can't be refreshed; AuthContext clears the user. */
export const AUTH_EXPIRED_EVENT = 'auth:expired'

/**
 * Primary axios instance for API requests. It attaches the in-memory access
 * token, coalesces identical in-flight GETs (opt out with `{ dedupe: false }`)
 * and refreshes the session once on a 401.
 */
const api = axios.create({
  baseURL: baseApiUrl,
  withCredentials: true,
})

/** For auth endpoints (refresh) that must bypass the 401 interceptor to avoid loops. */
export const authApi = axios.create({ baseURL: baseApiUrl, withCredentials: true })

/**
 * Identical GETs in flight share one network request (React.StrictMode
 * double-mounts, several components asking for the same resource).
 */
const inflightGets = new Map()

const inflightKey = (url, config) => {
  const base = api.defaults.baseURL || ''
  const params = config && config.params ? JSON.stringify(config.params) : ''
  return `GET:${base}${url}?params=${params}`
}

const sendGet = api.get.bind(api)

api.get = (url, config = {}) => {
  const isDedupeEnabled = config.dedupe !== false
  if (!isDedupeEnabled) return sendGet(url, config)

  const key = inflightKey(url, config)
  if (inflightGets.has(key)) return inflightGets.get(key)

  const request = sendGet(url, config).finally(() => {
    inflightGets.delete(key)
  })
  inflightGets.set(key, request)
  return request
}

/** In-memory access token (never persisted). */
let accessToken = null

export function setAccessToken(token) {
  accessToken = token
}

export default api

/**
 * Origin the backend is served from, without the trailing /api. '' means same
 * origin, so imageUrl() yields root-relative paths like '/uploads/x.png'.
 */
export const getApiRoot = () => {
  const base = api.defaults.baseURL || baseApiUrl
  return base.replace(API_PATH_SUFFIX, '')
}

const withBearer = (config, token) => {
  config.headers = config.headers || {}
  config.headers.Authorization = `Bearer ${token}`
  return config
}

api.interceptors.request.use(config => (accessToken ? withBearer(config, accessToken) : config))

const extractToken = (data) =>
  data?.accessToken || data?.AccessToken || data?.user?.accessToken || null

/** Name of the Web Lock that serializes refreshes across tabs of this origin. */
export const REFRESH_LOCK_NAME = 'dc-auth-refresh'
/**
 * A refresh that got 401 is retried once after this delay before the session
 * counts as expired (see refreshSession).
 */
export const REFRESH_RETRY_DELAY_MS = 300

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms))

const postRefresh = async () => {
  const response = await authApi.post('/auth/refresh')
  const token = extractToken(response.data)
  if (!token) throw new Error('Refresh response had no access token')
  setAccessToken(token)
  return token
}

const isUnauthorized = error => error?.response?.status === UNAUTHORIZED

/**
 * The backend rotates the refresh cookie on every call and rejects a reused one
 * with 401. If another tab refreshed between our cookie being read and sent,
 * the cookie jar already holds the rotated cookie: try once more before giving up.
 */
const refreshWithRetry = async () => {
  try {
    return await postRefresh()
  } catch (err) {
    if (!isUnauthorized(err)) throw err
    await sleep(REFRESH_RETRY_DELAY_MS)
    return postRefresh()
  }
}

/**
 * Run `fn` while holding the cross-tab refresh lock, so tabs refresh one after
 * another (each with the cookie the previous one received). Without the Web
 * Locks API, just run it (in-tab coalescing and the 401 retry still apply).
 */
const withRefreshLock = (fn) => {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  if (locks && typeof locks.request === 'function') {
    return locks.request(REFRESH_LOCK_NAME, () => fn())
  }
  return fn()
}

let refreshInFlight = null

/**
 * POST /auth/refresh, coalescing concurrent callers in this tab onto one
 * request and serializing refreshes across tabs (Web Locks). The refresh cookie
 * rotates on every call, so two parallel refreshes (React StrictMode double
 * effects, app start racing a 401 retry, two tabs waking up together) would
 * otherwise race each other and log one of them out. Resolves to the new
 * access token (already stored); rejects if the refresh failed or returned no
 * token.
 */
export function refreshSession() {
  if (!refreshInFlight) {
    refreshInFlight = Promise.resolve()
      .then(() => withRefreshLock(refreshWithRetry))
      .finally(() => { refreshInFlight = null })
  }
  return refreshInFlight
}

/** Auth endpoints whose 401 means "bad credentials"/"no session", never "token expired". */
const NO_REFRESH_URL = /\/auth\/(login|register|refresh|logout)(\b|\/|\?|$)/

const notifyAuthExpired = () => {
  if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') {
    window.dispatchEvent(new Event(AUTH_EXPIRED_EVENT))
  }
}

/** Requests that got a 401 while the interceptor's refresh was running. */
let isRefreshing = false
let waitingRequests = []

const resolveWaitingRequests = (token) => {
  waitingRequests.forEach(waiting => waiting.resolve(token))
  waitingRequests = []
}

const rejectWaitingRequests = (error) => {
  waitingRequests.forEach(waiting => waiting.reject(error))
  waitingRequests = []
}

const retryWithToken = (request, token) => api.request(withBearer(request, token))

const canRefreshFor = (error, request) =>
  isUnauthorized(error) && !request._retry && !NO_REFRESH_URL.test(request.url || '')

/** Waits for the refresh in flight; on failure surfaces this request's own 401. */
const retryAfterRefreshInFlight = (request, error) =>
  new Promise((resolve, reject) => {
    waitingRequests.push({ resolve, reject })
  }).then(
    token => retryWithToken(request, token),
    () => Promise.reject(error)
  )

const refreshAndRetry = async (request, error) => {
  isRefreshing = true
  let newToken = null
  try {
    newToken = await refreshSession()
    resolveWaitingRequests(newToken)
  } catch (refreshError) {
    rejectWaitingRequests(refreshError)
    setAccessToken(null)
    notifyAuthExpired()
    return Promise.reject(error)
  } finally {
    isRefreshing = false
  }
  return retryWithToken(request, newToken)
}

const handleResponseError = async (error) => {
  const request = error.config
  if (!request || !canRefreshFor(error, request)) return Promise.reject(error)

  request._retry = true
  return isRefreshing ? retryAfterRefreshInFlight(request, error) : refreshAndRetry(request, error)
}

api.interceptors.response.use(response => response, handleResponseError)
