import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function useRequireAdmin() {
  const { user, loading } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (!loading && (!user || !user.isAdmin)) {
      navigate('/')
    }
  }, [loading, user, navigate])

  return { user, loading, isAuthorized: !loading && user?.isAdmin }
}
