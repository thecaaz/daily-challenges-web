import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

const CONFLICT = 409
const DEFAULT_LEVEL = 1

/** The submission already succeeded; a failed profile refresh isn't fatal. */
const ignoreProfileRefreshError = () => null

const isConflict = err => err?.response?.status === CONFLICT

function successMessage(xpGain, reachedLevel) {
  const hasXp = xpGain > 0
  if (reachedLevel) return hasXp ? `Submitted! +${xpGain} XP ⬆️ Level ${reachedLevel}!` : `Level ${reachedLevel}!`
  return hasXp ? `Submitted! +${xpGain} XP` : 'Submitted!'
}

function submissionFormData(gameId, score, screenshot) {
  const formData = new FormData()
  formData.append('gameId', gameId)
  formData.append('score', score)
  formData.append('screenshot', screenshot)
  return formData
}

/**
 * Posts a score submission and reports the outcome (XP, level-up, errors)
 * through the snackbar. The score is sent as typed: the server parses it with
 * the shared score spec, and re-serialising a Number would turn huge values
 * into "1e+21".
 *
 * With an `onSubmitted({ submission, xpGain, duplicate? })` handler the caller
 * decides what happens next, and a 409 (already submitted) counts as a
 * duplicate success; without one the user is taken to the game.
 *
 * @param {string|number} gameId
 * @param {Function} [onSubmitted]
 * @returns {{ submit: (score: string, screenshot: File) => Promise<void>, isSubmitting: boolean }}
 */
export default function useSubmitScore(gameId, onSubmitted) {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const { user, fetchMe } = useAuth()
  const [isSubmitting, setIsSubmitting] = useState(false)
  const hasSubmittedHandler = typeof onSubmitted === 'function'

  const refreshProfile = () => fetchMe().catch(ignoreProfileRefreshError)

  const handleSubmitted = async ({ submission, xpGain } = {}) => {
    const previousLevel = user?.level ?? DEFAULT_LEVEL
    const updatedUser = await refreshProfile()
    const reachedLevel = updatedUser && updatedUser.level > previousLevel ? updatedUser.level : null
    showSnackbar(successMessage(xpGain, reachedLevel), 'success')

    if (hasSubmittedHandler) onSubmitted({ submission, xpGain })
    else navigate(`/games/${gameId}`)
  }

  const handleDuplicate = async (data) => {
    await refreshProfile()
    onSubmitted({ submission: data?.submission || null, xpGain: data?.xpGain || 0, duplicate: true })
  }

  const submit = async (score, screenshot) => {
    setIsSubmitting(true)
    try {
      const res = await api.post('/submissions', submissionFormData(gameId, score, screenshot))
      await handleSubmitted(res.data || {})
    } catch (err) {
      if (isConflict(err) && hasSubmittedHandler) await handleDuplicate(err.response.data)
      else showSnackbar(getApiErrorMessage(err, 'Failed to submit'), 'error')
    } finally {
      setIsSubmitting(false)
    }
  }

  return { submit, isSubmitting }
}
