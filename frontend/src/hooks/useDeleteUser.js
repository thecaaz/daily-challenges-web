import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * Returns `deleteUser(userId)`: asks for confirmation, deletes the user and
 * then calls `onDeleted()`.
 *
 * @param {object} options
 * @param {(dialog: { title: string, message: string }) => Promise<boolean>} options.confirm
 * @param {() => void} options.onDeleted
 */
export default function useDeleteUser({ confirm, onDeleted }) {
  const { showSnackbar } = useSnackbar()

  return async userId => {
    const isConfirmed = await confirm({ title: 'Delete user?', message: 'This will permanently delete this user. This cannot be undone.' })
    if (!isConfirmed) return
    try {
      await api.delete(`/admin/users/${userId}`)
      showSnackbar('User deleted', 'success')
      onDeleted()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to delete user'), 'error')
    }
  }
}
