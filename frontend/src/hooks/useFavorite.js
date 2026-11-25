import { useState, useEffect } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import { withRedirect } from '../utils/safeRedirect'

/**
 * Optimistic favourite toggle for a game; anonymous users are sent to log in.
 * The local state follows `initial` when the parent updates it.
 *
 * @returns {{ isFavorite: boolean, toggle: () => Promise<void>, loading: boolean }}
 */
export default function useFavorite(gameId, initial = false) {
  const [isFavorite, setIsFavorite] = useState(Boolean(initial))
  const [loading, setLoading] = useState(false)
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { showSnackbar } = useSnackbar()

  const toggle = async () => {
    if (!user) {
      navigate(withRedirect('/login', `${location.pathname}${location.search}`))
      return
    }

    const next = !isFavorite
    setIsFavorite(next)
    window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { gameId, isFavorite: next, optimistic: true } }))
    setLoading(true)
    try {
      if (next) {
        await api.post(`/favorites/${gameId}`)
      } else {
        await api.delete(`/favorites/${gameId}`)
      }
      window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { gameId, isFavorite: next, confirmed: true } }))
    } catch (err) {
      setIsFavorite(!next)
      window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { gameId, isFavorite: !next } }))
      console.error('Failed to toggle favorite', err)
      showSnackbar('Failed to update favorite', 'error')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    setIsFavorite(Boolean(initial))
  }, [initial])

  return { isFavorite, toggle, loading }
}
