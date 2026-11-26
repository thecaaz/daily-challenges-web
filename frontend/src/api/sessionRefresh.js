import { authApi } from './clients'
import { extractAccessToken, setAccessToken } from './accessToken'
import { isUnauthorizedError } from './httpStatus'

/** Name of the Web Lock that serializes refreshes across tabs of this origin. */
export const REFRESH_LOCK_NAME = 'dc-auth-refresh'
/**
 * A refresh that got 401 is retried once after this delay before the session
 * counts as expired (see refreshSession).
 */
export const REFRESH_RETRY_DELAY_MS = 300

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

/**
 * The backend rotates the refresh cookie on every call and rejects a reused one
 * with 401. If another tab refreshed between our cookie being read and sent,
 * the cookie jar already holds the rotated cookie: try once more before giving up.
 */
const refreshWithRetry = async () => {
  try {
    return await postRefresh()
  } catch (err) {
    if (!isUnauthorizedError(err)) throw err
    await sleep(REFRESH_RETRY_DELAY_MS)
    return postRefresh()
  }
}

const postRefresh = async () => {
  const response = await authApi.post('/auth/refresh')
  const token = extractAccessToken(response.data)
  if (!token) throw new Error('Refresh response had no access token')
  setAccessToken(token)
  return token
}

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms))
