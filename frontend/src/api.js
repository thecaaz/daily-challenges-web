import axios from 'axios'

const backendBase = (typeof window !== 'undefined' && window.__BACKEND_URL)
  ? window.__BACKEND_URL
  : 'http://localhost:5000'

const baseApiUrl = `${backendBase.replace(/\/$/, '')}/api`

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

export const getApiRoot = () => {
  const base = api.defaults.baseURL || 'http://localhost:5000'
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

api.interceptors.response.use(
  res => res,
  async (error) => {
    const originalRequest = error.config
    if (!originalRequest) return Promise.reject(error)

    if (error.response && error.response.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject })
        }).then(token => {
          originalRequest.headers.Authorization = 'Bearer ' + token
          return api.request(originalRequest)
        }).catch(err => Promise.reject(err))
      }

      originalRequest._retry = true
      isRefreshing = true

      try {
        const r = await authApi.post('/auth/refresh')
        const newToken = r.data.accessToken || r.data.AccessToken || (r.data && r.data.user && r.data.user.accessToken) // support variations
        if (newToken) setAccessToken(newToken)
        processQueue(null, newToken)
        originalRequest.headers.Authorization = 'Bearer ' + newToken
        return api.request(originalRequest)
      } catch (err) {
        processQueue(err, null)
        return Promise.reject(err)
      } finally {
        isRefreshing = false
      }
    }

    return Promise.reject(error)
  }
)
