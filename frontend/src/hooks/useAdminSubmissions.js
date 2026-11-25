import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import useLatestRequest from './useLatestRequest'

/**
 * The unfiltered submissions of one game, for admin moderation. Scores are
 * edited locally (`setScore`) and persisted per row (`save`).
 * Only the latest `show()` request may update the list, so switching games
 * quickly never shows another game's submissions.
 *
 * @param {(options: { title: string, message: string }) => Promise<boolean>} confirm
 */
export default function useAdminSubmissions(confirm) {
  const { showSnackbar } = useSnackbar()
  const startRequest = useLatestRequest()
  const [selectedGameId, setSelectedGameId] = useState(null)
  const [submissions, setSubmissions] = useState([])

  const show = async (gameId) => {
    const isLatest = startRequest()
    if (gameId !== selectedGameId) setSubmissions([])
    setSelectedGameId(gameId)
    try {
      const res = await api.get(`/submissions/game/${gameId}/unfiltered`)
      if (isLatest()) setSubmissions(res.data || [])
    } catch (err) {
      if (isLatest()) showSnackbar(getApiErrorMessage(err, 'Failed to load submissions'), 'error')
    }
  }

  const reload = () => show(selectedGameId)

  const closeIfShowing = (gameId) => {
    if (selectedGameId !== gameId) return
    setSelectedGameId(null)
    setSubmissions([])
  }

  const setScore = (submissionId, score) => {
    setSubmissions(prev => prev.map(s => (s.id === submissionId ? { ...s, score } : s)))
  }

  const save = async (submission) => {
    try {
      const trimmedScore = String(submission.score ?? '').trim()
      await api.put(`/submissions/${submission.id}`, { score: trimmedScore })
      showSnackbar('Submission updated', 'success')
      reload()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to update submission'), 'error')
    }
  }

  const remove = async (submissionId) => {
    const confirmed = await confirm({ title: 'Delete submission?', message: 'This submission will be permanently removed.' })
    if (!confirmed) return
    try {
      await api.delete(`/submissions/${submissionId}`)
      showSnackbar('Submission deleted', 'success')
      reload()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to delete submission'), 'error')
    }
  }

  return { selectedGameId, submissions, show, closeIfShowing, setScore, save, remove }
}
