import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import api, { refreshSession, setAccessToken, AUTH_EXPIRED_EVENT } from '../api'
import Loading from '../components/ui/Loading'

const AuthContext = createContext()

const UNAUTHORIZED = 401

const extractToken = (data) => data?.accessToken || data?.AccessToken || null

const isUnauthorized = (err) => err?.response?.status === UNAUTHORIZED

/** Resolves to whether the refresh cookie still holds a session (never rejects). */
const restoreSessionToken = () => refreshSession().then(() => true, () => false)

/**
 * Provides `{ user, loading, login, register, logout, fetchMe }`.
 *
 * Children render only after the session was restored from the refresh
 * cookie, so the first requests are already authenticated. `fetchMe` treats
 * only a 401 as "not logged in": any other failure (network blip, 5xx) keeps
 * the current user so a transient error doesn't log anyone out. `logout`
 * always drops the local session, even if the server call failed.
 */
export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)

  const clearSession = useCallback(() => {
    setAccessToken(null)
    setUser(null)
  }, [])

  const fetchMe = useCallback(async () => {
    try {
      const res = await api.get('/auth/me')
      setUser(res.data)
      return res.data
    } catch (err) {
      if (isUnauthorized(err)) setUser(null)
      return null
    }
  }, [])

  const loading = useRestoredSession(fetchMe)
  useAuthExpiredListener(clearSession)

  const login = useCallback(async (username, password) => {
    const res = await api.post('/auth/login', { username, password })
    const token = extractToken(res.data)
    if (token) setAccessToken(token)
    await fetchMe()
    return res.data
  }, [fetchMe])

  const register = useCallback(async (username, password) => {
    const res = await api.post('/auth/register', { username, password })
    return res.data
  }, [])

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout')
    } finally {
      clearSession()
    }
  }, [clearSession])

  const value = useMemo(
    () => ({ user, loading, login, register, logout, fetchMe }),
    [user, loading, login, register, logout, fetchMe]
  )

  return (
    <AuthContext.Provider value={value}>
      {loading ? <Loading /> : children}
    </AuthContext.Provider>
  )
}

/** Restores the session once on mount; returns true until that finished. */
function useRestoredSession(fetchMe) {
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    const restore = async () => {
      const hasSession = await restoreSessionToken()
      if (hasSession && !cancelled) await fetchMe()
      if (!cancelled) setLoading(false)
    }
    restore()
    return () => { cancelled = true }
  }, [fetchMe])

  return loading
}

/** The api client (src/api) fires AUTH_EXPIRED_EVENT when a 401 couldn't be fixed by refreshing. */
function useAuthExpiredListener(onExpired) {
  useEffect(() => {
    window.addEventListener(AUTH_EXPIRED_EVENT, onExpired)
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, onExpired)
  }, [onExpired])
}

export const useAuth = () => useContext(AuthContext)

export default AuthContext
