import api from './index'

/** Friend requests other users sent to the current user (empty list without a body). */
export function getIncomingFriendRequests() {
  return api.get('/friends/requests/incoming').then(response => response.data ?? [])
}

/** Friend requests the current user sent (empty list without a body). */
export function getSentFriendRequests() {
  return api.get('/friends/requests/sent').then(response => response.data ?? [])
}
