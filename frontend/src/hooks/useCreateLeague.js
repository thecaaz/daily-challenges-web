import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/** State of the "Create League" dialog; a created league is opened right away. */
export default function useCreateLeague() {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const [isOpen, setIsOpen] = useState(false)
  const [name, setName] = useState('')
  const [isCreating, setIsCreating] = useState(false)
  const trimmedName = name.trim()

  const create = async () => {
    if (!trimmedName || isCreating) return
    setIsCreating(true)
    try {
      const res = await api.post('/leagues', { name: trimmedName })
      showSnackbar(`League "${res.data.name}" created!`)
      setIsOpen(false)
      setName('')
      navigate(`/leagues/${res.data.id}`)
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to create league.'), 'error')
    } finally {
      setIsCreating(false)
    }
  }

  return {
    isOpen,
    open: () => setIsOpen(true),
    close: () => setIsOpen(false),
    name,
    setName,
    isCreating,
    canCreate: !isCreating && trimmedName !== '',
    create,
  }
}
