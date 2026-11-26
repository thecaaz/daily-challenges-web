import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

function joinUrlFor(token) {
  return `${window.location.origin}/leagues/join/${token}`
}

/**
 * Creates single-use league invite links and holds the dialog that shows them.
 * Calls `onCreated` after a link was created.
 */
export default function useInviteLink(leagueId, onCreated) {
  const { showSnackbar } = useSnackbar()
  const [link, setLink] = useState('')
  const [isOpen, setIsOpen] = useState(false)
  const [isCreating, setIsCreating] = useState(false)

  const create = async () => {
    if (isCreating) return
    setIsCreating(true)
    try {
      const res = await api.post(`/leagues/${leagueId}/invite-link`)
      setLink(joinUrlFor(res.data.token))
      setIsOpen(true)
      onCreated()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to create invite link.'), 'error')
    } finally {
      setIsCreating(false)
    }
  }

  return { link, isOpen, close: () => setIsOpen(false), isCreating, create }
}
