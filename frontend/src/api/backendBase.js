const DEV_BACKEND_URL = 'http://localhost:5000'
const TRAILING_SLASHES = /\/+$/
const API_PATH_SUFFIX = /\/api\/?$/

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

export const BASE_API_URL = `${backendBase}/api`

/** `apiUrl` without its trailing /api. */
export function withoutApiPath(apiUrl) {
  return apiUrl.replace(API_PATH_SUFFIX, '')
}
