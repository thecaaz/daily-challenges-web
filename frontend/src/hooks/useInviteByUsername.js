import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * State and submit handler for the "invite by username" dialog.
 * Calls `onInvited` after an invitation was sent.
 */
export default function useInviteByUsername(leagueId, onInvited) {
  const { showSnackbar } = useSnackbar()
  const [isOpen, setIsOpen] = useState(false)
  const [username, setUsername] = useState('')
  const [isSending, setIsSending] = useState(false)

  const send = async () => {
    const trimmedUsername = username.trim()
    if (!trimmedUsername || isSending) return
    setIsSending(true)
    try {
      await api.post(`/leagues/${leagueId}/invitations`, { username: trimmedUsername })
      showSnackbar(`Invitation sent to ${trimmedUsername}.`)
      setIsOpen(false)
      setUsername('')
      onInvited()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Could not send invitation.'), 'error')
    } finally {
      setIsSending(false)
    }
  }

  return {
    isOpen,
    open: () => setIsOpen(true),
    close: () => setIsOpen(false),
    username,
    setUsername,
    isSending,
    send,
  }
}
