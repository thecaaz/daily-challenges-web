import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * State and submit handler for the rename-league dialog.
 * `open(currentName)` pre-fills the field; `onRenamed(name)` receives the
 * name the backend stored.
 */
export default function useRenameLeague(leagueId, onRenamed) {
  const { showSnackbar } = useSnackbar()
  const [isOpen, setIsOpen] = useState(false)
  const [name, setName] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  const open = currentName => {
    setName(currentName)
    setIsOpen(true)
  }

  const save = async () => {
    const trimmedName = name.trim()
    if (!trimmedName || isSaving) return
    setIsSaving(true)
    try {
      const res = await api.patch(`/leagues/${leagueId}/name`, { name: trimmedName })
      onRenamed(res.data.name)
      showSnackbar('League renamed.')
      setIsOpen(false)
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to rename league.'), 'error')
    } finally {
      setIsSaving(false)
    }
  }

  return { isOpen, open, close: () => setIsOpen(false), name, setName, isSaving, save }
}
