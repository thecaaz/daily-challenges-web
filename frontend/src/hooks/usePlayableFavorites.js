import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api'
import { hasAdapterForUrl } from '../utils/adapters'

const EMPTY = Object.freeze({ total: 0, submittedCount: 0, pendingGames: [], playable: [] })

/**
 * Favorite games for the current user, split into done / pending, plus the
 * pending ones the ScoreBridge extension can capture ("playable").
 *
 * - The favorites summary is shown as soon as /games returns; the extension
 *   checks run afterwards, in parallel (`checkingPlayable` is true meanwhile).
 * - Out-of-order responses are dropped with a request counter.
 * - With `subscribe` (default true) it reloads on `favorite-changed` events,
 *   ignoring the optimistic ones (the confirmed or rolled-back event follows).
 *
 * @param {number|string|null|undefined} userId - reload when it changes; null clears.
 * @param {object} [options]
 * @param {boolean} [options.enabled=true] - false while auth is still loading.
 * @param {boolean} [options.subscribe=true]
 */
export default function usePlayableFavorites(userId, { enabled = true, subscribe = true } = {}) {
  const [state, setState] = useState(EMPTY)
  const [loading, setLoading] = useState(true)
  const [checkingPlayable, setCheckingPlayable] = useState(false)
  const [error, setError] = useState(false)
  const requestRef = useRef(0)

  const load = useCallback(async () => {
    const requestId = ++requestRef.current
    const isCurrent = () => requestRef.current === requestId

    if (userId == null) {
      setState(EMPTY)
      setLoading(false)
      setCheckingPlayable(false)
      setError(false)
      return
    }

    let favs
    try {
      const res = await api.get('/games')
      const all = Array.isArray(res.data) ? res.data : []
      favs = all.filter(g => g.isFavorite)
    } catch (e) {
      if (!isCurrent()) return
      setState(EMPTY)
      setError(true)
      setLoading(false)
      setCheckingPlayable(false)
      return
    }
    if (!isCurrent()) return

    const pendingGames = favs.filter(g => !g.hasSubmittedForLatest)
    setState({ total: favs.length, submittedCount: favs.length - pendingGames.length, pendingGames, playable: [] })
    setError(false)
    setLoading(false)

    const candidates = pendingGames.filter(g => g.url)
    if (candidates.length === 0) {
      setCheckingPlayable(false)
      return
    }
    setCheckingPlayable(true)
    const checks = await Promise.all(candidates.map(g => hasAdapterForUrl(g.url).then(Boolean, () => false)))
    if (!isCurrent()) return
    const playable = candidates
      .filter((_, i) => checks[i])
      .sort((a, b) => Number(a.id) - Number(b.id))
    setState(prev => ({ ...prev, playable }))
    setCheckingPlayable(false)
  }, [userId])

  useEffect(() => {
    if (!enabled) return undefined
    const counter = requestRef
    load()
    return () => { counter.current++ } // drop in-flight responses
  }, [enabled, load])

  useEffect(() => {
    if (!enabled || !subscribe || userId == null) return undefined
    const onChanged = (ev) => {
      if (ev?.detail?.optimistic) return
      load()
    }
    window.addEventListener('favorite-changed', onChanged)
    return () => window.removeEventListener('favorite-changed', onChanged)
  }, [enabled, subscribe, userId, load])

  return {
    ...state,
    playableCount: state.playable.length,
    loading: enabled ? loading : true,
    checkingPlayable,
    error,
    reload: load,
  }
}
