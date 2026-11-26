import { useState, useEffect, useCallback } from 'react'
import api from '../api'
import useLatestRequest from './useLatestRequest'
import { onFavoriteChange } from '../utils/favoriteEvents'

const isSameGame = (a, b) => String(a) === String(b)

/** Copy of `dashboard` with the favourite flag of `gameId` set in every list. */
export function applyFavoriteChange(dashboard, { gameId, isFavorite }) {
  if (!dashboard) return dashboard
  return {
    ...dashboard,
    recentGames: (dashboard.recentGames || []).map(activity =>
      isSameGame(activity.game?.id, gameId) ? { ...activity, game: { ...activity.game, isFavorite } } : activity
    ),
    friendActivity: (dashboard.friendActivity || []).map(activity =>
      isSameGame(activity.gameId, gameId) ? { ...activity, isFavorite } : activity
    ),
  }
}

/**
 * Display values for the dashboard. The server's totals are preferred over
 * counting the lists, which are capped at 10 items.
 */
export function summarizeDashboard(dashboard) {
  const { recentGames = [], friendActivity = [], friends = [], xpEarnedToday = 0, userTodayRanks = [] } = dashboard
  const activeGameCount = Number.isFinite(dashboard.activeGameCount) ? dashboard.activeGameCount : recentGames.length
  const submissionCountToday = Number.isFinite(dashboard.submissionCountToday)
    ? dashboard.submissionCountToday
    : recentGames.reduce((sum, activity) => sum + (activity.todayCount || 0), 0)
  return { recentGames, friendActivity, friends, xpEarnedToday, userTodayRanks, activeGameCount, submissionCountToday }
}

/**
 * The /dashboard summary for the current user.
 *
 * - Reloads when `userId` changes; only the latest request may update state.
 * - A failed load clears `summary` and sets `error`; `retry()` loads again.
 * - Favourites toggled anywhere (`favorite-changed` events) are applied to
 *   the loaded lists without a refetch.
 *
 * @returns {{ summary: ReturnType<typeof summarizeDashboard>|null, loading: boolean, error: boolean, retry: () => Promise<void> }}
 */
export default function useDashboard(userId) {
  const startRequest = useLatestRequest()
  const [dashboard, setDashboard] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  const load = useCallback(async () => {
    const isLatest = startRequest()
    setLoading(true)
    setError(false)
    try {
      const response = await api.get('/dashboard')
      if (isLatest()) setDashboard(response.data || {})
    } catch {
      if (!isLatest()) return
      setDashboard(null)
      setError(true)
    } finally {
      if (isLatest()) setLoading(false)
    }
  }, [startRequest])

  const ignorePendingResponse = useCallback(() => { startRequest() }, [startRequest])

  useEffect(() => {
    load()
    return ignorePendingResponse
  }, [load, ignorePendingResponse, userId])

  useEffect(() => onFavoriteChange(change => {
    if (typeof change.gameId === 'undefined') return
    setDashboard(prev => applyFavoriteChange(prev, change))
  }), [])

  const summary = dashboard ? summarizeDashboard(dashboard) : null

  return { summary, loading, error, retry: load }
}
