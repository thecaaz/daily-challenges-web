import axios from 'axios'
import { BASE_API_URL } from './backendBase'

/**
 * Primary axios instance for API requests. index.js makes it attach the
 * in-memory access token, coalesce identical in-flight GETs (opt out with
 * `{ dedupe: false }`) and refresh the session once on a 401.
 */
export const api = axios.create({
  baseURL: BASE_API_URL,
  withCredentials: true,
})

/** For auth endpoints (refresh) that must bypass the 401 interceptor to avoid loops. */
export const authApi = axios.create({ baseURL: BASE_API_URL, withCredentials: true })
