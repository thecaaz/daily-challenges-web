import { useEffect } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function useRequireAuth() {
  const { user, loading, fetchMe } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    if (!loading && !user) {
      const redirect = `${location.pathname}${location.search}`
      navigate(`/login?redirect=${encodeURIComponent(redirect)}`, { replace: true })
    }
  }, [loading, user, navigate, location.pathname, location.search])

  return { user, loading, fetchMe }
}
