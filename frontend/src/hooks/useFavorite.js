import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'

export default function useFavorite(gameId, initial = false) {
  const [isFavorite, setIsFavorite] = useState(Boolean(initial))
  const [loading, setLoading] = useState(false)
  const { user } = useAuth()
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()

  const toggle = async () => {
    if (!user) {
      navigate('/login')
      return
    }

    const next = !isFavorite
    setIsFavorite(next)
    window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { gameId, isFavorite: next } }))
    setLoading(true)
    try {
      if (next) {
        await api.post(`/favorites/${gameId}`)
      } else {
        await api.delete(`/favorites/${gameId}`)
      }
    } catch (err) {
      setIsFavorite(!next)
      window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { gameId, isFavorite: !next } }))
      console.error('Failed to toggle favorite', err)
      showSnackbar('Failed to update favorite', 'error')
    } finally {
      setLoading(false)
    }
  }

  return { isFavorite, toggle, loading }
}
