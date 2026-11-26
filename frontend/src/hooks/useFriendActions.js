import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import useBusyKeys from './useBusyKeys'

const requestKey = friendRequest => `req:${friendRequest.id}`
const friendKey = friend => `friend:${friend.userId}`

/**
 * Accept, decline and cancel friend requests, and remove friends.
 *
 * Each row's buttons are disabled while its own request is in flight;
 * `onChanged` runs after every successful action.
 *
 * @param {object} options
 * @param {() => Promise<void>} options.onChanged
 * @param {(dialog: object) => Promise<boolean>} options.confirm - from useConfirm.
 */
export default function useFriendActions({ onChanged, confirm }) {
  const { showSnackbar } = useSnackbar()
  const { isBusy, runExclusive } = useBusyKeys()

  const runRowAction = (key, { request, successMessage, errorFallback }) => runExclusive(key, async () => {
    try {
      await request()
      showSnackbar(successMessage)
      await onChanged()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, errorFallback), 'error')
    }
  })

  const accept = friendRequest => runRowAction(requestKey(friendRequest), {
    request: () => api.post(`/friends/requests/${friendRequest.id}/accept`),
    successMessage: `You are now friends with ${friendRequest.senderUsername}.`,
    errorFallback: 'Failed to accept request.',
  })

  const decline = friendRequest => runRowAction(requestKey(friendRequest), {
    request: () => api.post(`/friends/requests/${friendRequest.id}/reject`),
    successMessage: 'Friend request declined.',
    errorFallback: 'Failed to decline request.',
  })

  const cancel = friendRequest => runRowAction(requestKey(friendRequest), {
    request: () => api.delete(`/friends/requests/${friendRequest.id}`),
    successMessage: 'Friend request cancelled.',
    errorFallback: 'Failed to cancel request.',
  })

  const removeFriend = async friend => {
    const isConfirmed = await confirm({
      title: 'Remove friend?',
      message: `Remove ${friend.username} from your friends?`,
      confirmText: 'Remove',
    })
    if (!isConfirmed) return
    await runRowAction(friendKey(friend), {
      request: () => api.delete(`/friends/${friend.userId}`),
      successMessage: `Removed ${friend.username} from friends.`,
      errorFallback: 'Failed to remove friend.',
    })
  }

  return {
    accept,
    decline,
    cancel,
    removeFriend,
    isRequestBusy: friendRequest => isBusy(requestKey(friendRequest)),
    isFriendBusy: friend => isBusy(friendKey(friend)),
  }
}
