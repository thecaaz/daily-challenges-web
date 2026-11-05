import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import api, { refreshSession, setAccessToken, AUTH_EXPIRED_EVENT } from '../api'
import Loading from '../components/ui/Loading'

const AuthContext = createContext()

const extractToken = (data) => data?.accessToken || data?.AccessToken || null

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [loading, setLoading] = useState(true)

  // Load the current user. Only a 401 means "not logged in"; any other failure
  // (network blip, 5xx) keeps the current user so a transient error doesn't log
  // anyone out.
  const fetchMe = useCallback(async () => {
    try {
      const res = await api.get('/auth/me')
      setUser(res.data)
      return res.data
    } catch (err) {
      if (err?.response?.status === 401) {
        setUser(null)
        return null
      }
      return null
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    // Restore the session from the refresh cookie before rendering the app, so
    // the first requests are already authenticated.
    const restore = async () => {
      try {
        await refreshSession()
        if (!cancelled) await fetchMe()
      } catch {
        // Not logged in (no or expired refresh cookie).
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    restore()
    return () => { cancelled = true }
  }, [fetchMe])

  // api.js fires this when a 401 couldn't be fixed by refreshing.
  useEffect(() => {
    const onExpired = () => {
      setAccessToken(null)
      setUser(null)
    }
    window.addEventListener(AUTH_EXPIRED_EVENT, onExpired)
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, onExpired)
  }, [])

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
      // Always drop the local session, even if the server call failed.
      setAccessToken(null)
      setUser(null)
    }
  }, [])

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

export const useAuth = () => useContext(AuthContext)

export default AuthContext
