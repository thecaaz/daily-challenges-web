import { useState, useEffect, useCallback } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'

const EMPTY_LISTS = { incoming: [], sent: [], friends: [] }

async function getFriendLists() {
  const [incomingRes, sentRes, friendsRes] = await Promise.all([
    api.get('/friends/requests/incoming'),
    api.get('/friends/requests/sent'),
    api.get('/friends'),
  ])
  return {
    incoming: incomingRes.data ?? [],
    sent: sentRes.data ?? [],
    friends: friendsRes.data ?? [],
  }
}

/**
 * The current user's incoming and sent friend requests and friends.
 *
 * `loading` is only true until the first load finishes; `refresh()` reloads
 * after an action without replacing the lists by a spinner.
 *
 * @returns {{ incoming: object[], sent: object[], friends: object[], loading: boolean, refresh: () => Promise<void> }}
 */
export default function useFriends() {
  const { showSnackbar } = useSnackbar()
  const [lists, setLists] = useState(EMPTY_LISTS)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    try {
      setLists(await getFriendLists())
    } catch {
      showSnackbar('Failed to load friends data.', 'error')
    } finally {
      setLoading(false)
    }
  }, [showSnackbar])

  useEffect(() => { refresh() }, [refresh])

  return { ...lists, loading, refresh }
}
