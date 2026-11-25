import { api } from './clients'
import { setAccessToken, withBearer } from './accessToken'
import { refreshSession, isUnauthorized } from './sessionRefresh'

/** Fired on window when the session can't be refreshed; AuthContext clears the user. */
export const AUTH_EXPIRED_EVENT = 'auth:expired'

/** Auth endpoints whose 401 means "bad credentials"/"no session", never "token expired". */
const NO_REFRESH_URL = /\/auth\/(login|register|refresh|logout)(\b|\/|\?|$)/

/** Requests that got a 401 while the interceptor's refresh was running. */
let isRefreshing = false
let waitingRequests = []

/**
 * Response error interceptor: on a 401 refreshes the session once and retries
 * the request with the new token; requests failing meanwhile wait for that
 * refresh. When the refresh fails the token is cleared and
 * AUTH_EXPIRED_EVENT fires.
 */
export async function refreshOnUnauthorized(error) {
  const request = error.config
  if (!request || !canRefreshFor(error, request)) return Promise.reject(error)

  request._retry = true
  return isRefreshing ? retryAfterRefreshInFlight(request, error) : refreshAndRetry(request, error)
}

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

const retryWithToken = (request, token) => api.request(withBearer(request, token))

const resolveWaitingRequests = (token) => {
  waitingRequests.forEach(waiting => waiting.resolve(token))
  waitingRequests = []
}

const rejectWaitingRequests = (error) => {
  waitingRequests.forEach(waiting => waiting.reject(error))
  waitingRequests = []
}

const notifyAuthExpired = () => {
  if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') {
    window.dispatchEvent(new Event(AUTH_EXPIRED_EVENT))
  }
}
