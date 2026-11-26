import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import { isValidPasswordLength, PASSWORD_LENGTH_MESSAGE } from '../utils/credentialRules'

/**
 * State of the admin "Set Password" dialog. Validation and API errors are
 * shown in the dialog; editing the password clears them.
 */
export default function useSetPasswordDialog() {
  const { showSnackbar } = useSnackbar()
  const [userId, setUserId] = useState(null)
  const [password, setPasswordValue] = useState('')
  const [error, setError] = useState('')

  const reset = nextUserId => {
    setUserId(nextUserId)
    setPasswordValue('')
    setError('')
  }

  const setPassword = value => {
    setPasswordValue(value)
    setError('')
  }

  const submit = async () => {
    if (!isValidPasswordLength(password)) {
      setError(PASSWORD_LENGTH_MESSAGE)
      return
    }
    try {
      await api.post(`/admin/users/${userId}/password`, { newPassword: password })
      showSnackbar('Password updated', 'success')
      reset(null)
    } catch (err) {
      setError(getApiErrorMessage(err, 'Failed to set password'))
    }
  }

  return {
    isOpen: userId !== null,
    open: reset,
    close: () => reset(null),
    password,
    setPassword,
    error,
    submit,
  }
}
