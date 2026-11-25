import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * Joins the league of an invite-link `token` and opens it.
 *
 * - Waits for auth to restore from the refresh cookie; a logged-out user is
 *   sent to login and comes back here afterwards.
 * - Each token is joined at most once, even if the effect re-runs
 *   (StrictMode, user object refreshed by fetchMe, ...).
 *
 * @returns {{ hasFailed: boolean }}
 */
export default function useJoinLeague(token) {
  const { user, loading } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()
  const [hasFailed, setHasFailed] = useState(false)
  const joinedTokenRef = useRef(null)

  useEffect(() => {
    if (loading) return
    if (!user) {
      navigate(`/login?redirect=${encodeURIComponent(`/leagues/join/${token}`)}`, { replace: true })
      return
    }
    if (joinedTokenRef.current === token) return
    joinedTokenRef.current = token

    api.post(`/leagues/join/${token}`)
      .then(res => {
        showSnackbar(`You joined "${res.data.name}"!`)
        navigate(`/leagues/${res.data.id}`)
      })
      .catch(err => {
        showSnackbar(getApiErrorMessage(err, 'Invalid or expired invite link.'), 'error')
        setHasFailed(true)
      })
  }, [token, user, loading, navigate, showSnackbar])

  return { hasFailed }
}
