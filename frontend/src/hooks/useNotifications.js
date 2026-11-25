import { useState, useEffect, useCallback } from 'react'
import api from '../api'

const POLL_INTERVAL_MS = 30_000
const RECENT_NOTIFICATIONS_URL = '/notifications?page=1&pageSize=10'

const ignoreBackgroundRefreshError = () => {}
const ignoreReadReceiptError = () => {}

const decrementFloorZero = count => Math.max(0, count - 1)
const increment = count => count + 1
const markedRead = notification => ({ ...notification, isRead: true })

/**
 * The signed-in user's notifications: an unread count polled every 30 s,
 * the recent notifications (loaded on demand) and actions on them.
 *
 * Polling and read receipts are best effort and fail silently. `remove` is
 * optimistic: the notification is put back where it was if the delete fails.
 *
 * @param {object|null} user - no polling and a zero count while null
 */
export default function useNotifications(user) {
  const [unreadCount, setUnreadCount] = useState(0)
  const [notifications, setNotifications] = useState([])

  useUnreadCountPolling(user, setUnreadCount)

  const loadRecent = async () => {
    try {
      const res = await api.get(RECENT_NOTIFICATIONS_URL)
      setNotifications(res.data.items ?? [])
      setUnreadCount(res.data.unreadCount ?? 0)
    } catch (error) {
      ignoreBackgroundRefreshError(error)
    }
  }

  const markRead = async (notification) => {
    if (notification.isRead) return
    try {
      await api.post(`/notifications/${notification.id}/read`)
      setUnreadCount(decrementFloorZero)
      setNotifications(prev => prev.map(x => (x.id === notification.id ? markedRead(x) : x)))
    } catch (error) {
      ignoreReadReceiptError(error)
    }
  }

  const markAllRead = async () => {
    try {
      await api.post('/notifications/read-all')
      setUnreadCount(0)
      setNotifications(prev => prev.map(markedRead))
    } catch (error) {
      ignoreReadReceiptError(error)
    }
  }

  const remove = async (notification) => {
    const index = notifications.findIndex(x => x.id === notification.id)
    setNotifications(prev => prev.filter(x => x.id !== notification.id))
    if (!notification.isRead) setUnreadCount(decrementFloorZero)
    try {
      await api.delete(`/notifications/${notification.id}`)
    } catch {
      setNotifications(prev => reinsertAt(prev, notification, index))
      if (!notification.isRead) setUnreadCount(increment)
    }
  }

  return { unreadCount, notifications, loadRecent, markRead, markAllRead, remove }
}

function useUnreadCountPolling(user, setUnreadCount) {
  const fetchUnreadCount = useCallback(async () => {
    if (!user) return
    try {
      const res = await api.get('/notifications/unread-count')
      setUnreadCount(res.data.unreadCount ?? 0)
    } catch (error) {
      ignoreBackgroundRefreshError(error)
    }
  }, [user, setUnreadCount])

  useEffect(() => {
    if (!user) {
      setUnreadCount(0)
      return
    }
    fetchUnreadCount()
    const timer = setInterval(fetchUnreadCount, POLL_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [user, fetchUnreadCount, setUnreadCount])
}

function reinsertAt(list, item, index) {
  if (list.some(x => x.id === item.id)) return list
  const position = index >= 0 ? Math.min(index, list.length) : list.length
  return [...list.slice(0, position), item, ...list.slice(position)]
}
