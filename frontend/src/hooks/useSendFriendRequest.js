import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * State and submit handler for "add friend by username".
 * Calls `onSent` after a request was sent.
 */
export default function useSendFriendRequest(onSent) {
  const { showSnackbar } = useSnackbar()
  const [username, setUsername] = useState('')
  const [isSending, setIsSending] = useState(false)
  const trimmedUsername = username.trim()

  const send = async () => {
    if (!trimmedUsername) return
    setIsSending(true)
    try {
      await api.post('/friends/requests/by-username', { username: trimmedUsername })
      showSnackbar(`Friend request sent to ${trimmedUsername}.`)
      setUsername('')
      onSent()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'User not found or request already exists.'), 'error')
    } finally {
      setIsSending(false)
    }
  }

  return {
    username,
    setUsername,
    isSending,
    canSend: !isSending && trimmedUsername !== '',
    send,
  }
}
