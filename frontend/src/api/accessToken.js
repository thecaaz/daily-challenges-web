/** In-memory access token (never persisted). */
let accessToken = null

export function setAccessToken(token) {
  accessToken = token
}

export function withBearer(config, token) {
  config.headers = config.headers || {}
  config.headers.Authorization = `Bearer ${token}`
  return config
}

/** Request interceptor: adds the current access token, if any. */
export function attachAccessToken(config) {
  return accessToken ? withBearer(config, accessToken) : config
}
