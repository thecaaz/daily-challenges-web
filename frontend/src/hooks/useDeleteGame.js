import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * Returns `deleteGame(gameId)`: asks for confirmation, deletes the game with
 * all its submissions and then calls `onDeleted(gameId)`.
 *
 * @param {object} options
 * @param {(options: { title: string, message: string }) => Promise<boolean>} options.confirm
 * @param {(gameId: number) => void} options.onDeleted
 */
export default function useDeleteGame({ confirm, onDeleted }) {
  const { showSnackbar } = useSnackbar()

  return async (gameId) => {
    const confirmed = await confirm({ title: 'Delete game?', message: 'This will permanently remove the game and all its submissions.' })
    if (!confirmed) return
    try {
      await api.delete(`/games/${gameId}`)
      showSnackbar('Game deleted', 'success')
      onDeleted(gameId)
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to delete game'), 'error')
    }
  }
}
