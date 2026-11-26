import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * The friendship actions towards one user, for the profile's friend button
 * (its status comes from `useFriendStatus`). Requests are looked up by user,
 * so a request that is already gone is silently skipped.
 *
 * `isBusy` is true while an action runs; `onChanged` runs after each one
 * succeeds, and failures are shown in the snackbar.
 *
 * @param {number} targetUserId
 * @param {object} options
 * @param {() => void} options.onChanged
 * @param {(dialog: object) => Promise<boolean>} options.confirm - from useConfirm.
 */
export default function useFriendRequestActions(targetUserId, { onChanged, confirm }) {
  const { showSnackbar } = useSnackbar()
  const [isBusy, setIsBusy] = useState(false)

  const run = async request => {
    setIsBusy(true)
    try {
      await request()
      onChanged()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Something went wrong.'), 'error')
    } finally {
      setIsBusy(false)
    }
  }

  const removeFriend = async () => {
    const isConfirmed = await confirm({
      title: 'Remove friend?',
      message: 'Remove this user from your friends?',
      confirmText: 'Remove',
    })
    if (isConfirmed) await run(() => api.delete(`/friends/${targetUserId}`))
  }

  return {
    isBusy,
    sendRequest: () => run(() => api.post('/friends/requests', { targetUserId })),
    cancelRequest: () => run(() => cancelSentRequestTo(targetUserId)),
    acceptRequest: () => run(() => acceptRequestFrom(targetUserId)),
    declineRequest: () => run(() => declineRequestFrom(targetUserId)),
    removeFriend,
  }
}

async function cancelSentRequestTo(targetUserId) {
  const response = await api.get('/friends/requests/sent')
  const sentRequest = response.data.find(request => request.receiverId === targetUserId)
  if (sentRequest) await api.delete(`/friends/requests/${sentRequest.id}`)
}

async function acceptRequestFrom(targetUserId) {
  const incomingRequest = await findIncomingRequestFrom(targetUserId)
  if (incomingRequest) await api.post(`/friends/requests/${incomingRequest.id}/accept`)
}

async function declineRequestFrom(targetUserId) {
  const incomingRequest = await findIncomingRequestFrom(targetUserId)
  if (incomingRequest) await api.post(`/friends/requests/${incomingRequest.id}/reject`)
}

async function findIncomingRequestFrom(targetUserId) {
  const response = await api.get('/friends/requests/incoming')
  return response.data.find(request => request.senderId === targetUserId)
}
