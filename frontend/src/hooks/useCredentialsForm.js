import { useState } from 'react'

/**
 * Username/password form state. On submit the username is trimmed and checked
 * with `validate(username, password)` (returns { username?, password? }
 * messages); only without errors is `onSubmit(username, password)` awaited.
 * Editing a field clears its error.
 */
export default function useCredentialsForm(validate, onSubmit) {
  const [values, setValues] = useState({ username: '', password: '' })
  const [errors, setErrors] = useState({})
  const [loading, setLoading] = useState(false)

  const changeField = field => event => {
    setValues(previous => ({ ...previous, [field]: event.target.value }))
    setErrors(previous => ({ ...previous, [field]: undefined }))
  }

  const handleSubmit = async event => {
    event.preventDefault()
    if (loading) return

    const username = values.username.trim()
    const nextErrors = validate(username, values.password)
    setErrors(nextErrors)
    const hasErrors = Object.keys(nextErrors).length > 0
    if (hasErrors) return

    setLoading(true)
    try {
      if (onSubmit) await onSubmit(username, values.password)
    } finally {
      setLoading(false)
    }
  }

  return {
    values,
    errors,
    loading,
    handleUsernameChange: changeField('username'),
    handlePasswordChange: changeField('password'),
    handleSubmit,
  }
}
