import { api, authApi } from './clients'
import { BASE_API_URL, withoutApiPath } from './backendBase'
import { attachAccessToken, setAccessToken } from './accessToken'
import dedupeIdenticalGets from './dedupeIdenticalGets'
import { refreshOnUnauthorized, AUTH_EXPIRED_EVENT } from './refreshOnUnauthorized'
import { refreshSession, REFRESH_LOCK_NAME, REFRESH_RETRY_DELAY_MS } from './sessionRefresh'

dedupeIdenticalGets(api)
api.interceptors.request.use(attachAccessToken)
api.interceptors.response.use(response => response, refreshOnUnauthorized)

/**
 * Origin the backend is served from, without the trailing /api. '' means same
 * origin, so imageUrl() yields root-relative paths like '/uploads/x.png'.
 */
export const getApiRoot = () => withoutApiPath(api.defaults.baseURL || BASE_API_URL)

export { resolveBackendBase } from './backendBase'
export {
  authApi,
  setAccessToken,
  refreshSession,
  AUTH_EXPIRED_EVENT,
  REFRESH_LOCK_NAME,
  REFRESH_RETRY_DELAY_MS,
}
export default api
