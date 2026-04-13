import React, { createContext, useContext, useEffect, useState } from 'react'
import api, { authApi, setAccessToken } from '../api'

const AuthContext = createContext()

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    // Attempt to refresh access token from refresh cookie on app start
    const tryRefresh = async () => {
      try {
        const r = await authApi.post('/auth/refresh')
        const token = r.data?.accessToken || r.data?.AccessToken
        if (token) {
          setAccessToken(token)
          await fetchMe()
        } else {
          await fetchMe()
        }
      } catch (e) {
        // no-op: not logged in
        await fetchMe()
      }
    }
    tryRefresh()
  }, [])

  const fetchMe = async () => {
    try {
      const res = await api.get('/auth/me')
      setUser(res.data)
      return res.data
    } catch (err) {
      setUser(null)
      return null
    } finally {
      setLoading(false)
    }
  }

  const login = async (username, password) => {
    const res = await api.post('/auth/login', { username, password })
    const token = res.data?.accessToken || res.data?.AccessToken
    if (token) setAccessToken(token)
    await fetchMe()
    return res.data
  }

  const register = async (username, password) => {
    const res = await api.post('/auth/register', { username, password })
    return res.data
  }

  const logout = async () => {
    await api.post('/auth/logout')
    setAccessToken(null)
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout, fetchMe }}>
      {children}
    </AuthContext.Provider>
  )
}

export const useAuth = () => useContext(AuthContext)

export default AuthContext
