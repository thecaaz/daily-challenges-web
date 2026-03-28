import axios from 'axios'

const backendBase = (typeof window !== 'undefined' && window.__BACKEND_URL)
  ? window.__BACKEND_URL
  : 'http://localhost:5000'

const api = axios.create({
  baseURL: `${backendBase.replace(/\/$/, '')}/api`,
  withCredentials: true,
})

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

export default api

export const getApiRoot = () => {
  const base = api.defaults.baseURL || 'http://localhost:5000'
  return base.replace(/\/api\/?$/, '')
}
