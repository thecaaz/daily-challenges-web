import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import { localToUtcHHmm } from '../utils/resetTime'
import toLocalResetTime from '../utils/toLocalResetTime'
import useImageUpload from './useImageUpload'

/** URL and description are always sent: an empty string clears the field. */
function buildGameFormData(game, utcResetTime, image) {
  const formData = new FormData()
  if (game.name) formData.append('name', game.name.trim())
  formData.append('url', (game.url ?? '').trim())
  formData.append('description', game.description ?? '')
  formData.append('resetTime', utcResetTime)
  formData.append('rankingMode', game.rankingMode ?? 'highest')
  if (image) formData.append('image', image)
  return formData
}

/**
 * Editing an existing game on the admin page. While a game is being edited,
 * pasted images go to the edit form.
 *
 * @param {object} options
 * @param {() => void} options.onSaved - called after the game was updated.
 */
export default function useGameEditor({ onSaved }) {
  const { showSnackbar } = useSnackbar()
  const [editingGame, setEditingGame] = useState(null)
  const isEditing = editingGame != null
  const image = useImageUpload(showSnackbar, { enabled: isEditing })

  const start = (game) => {
    image.clear()
    setEditingGame({ ...game, localResetTime: toLocalResetTime(game.resetTime) })
  }

  const cancel = () => {
    image.clear()
    setEditingGame(null)
  }

  const cancelIfEditing = (gameId) => {
    if (editingGame?.id === gameId) cancel()
  }

  const setField = (field, value) => setEditingGame(prev => ({ ...prev, [field]: value }))

  const save = async () => {
    const utcResetTime = localToUtcHHmm(editingGame.localResetTime)
    if (!utcResetTime) {
      showSnackbar('Enter a valid reset time', 'error')
      return
    }
    try {
      await api.put(`/games/${editingGame.id}`, buildGameFormData(editingGame, utcResetTime, image.screenshot))
      showSnackbar('Game updated', 'success')
      cancel()
      onSaved()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to update game'), 'error')
    }
  }

  return { editingGame, isEditing, start, cancel, cancelIfEditing, setField, save, image }
}
