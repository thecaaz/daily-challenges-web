import { useState, useEffect, useCallback } from 'react'
import { getGames } from '../api/games'
import { hasAdapterForUrl } from '../utils/adapters'
import { onFavoriteChange } from '../utils/favoriteEvents'
import useLatestRequest from './useLatestRequest'

const EMPTY = Object.freeze({ total: 0, submittedCount: 0, pendingGames: [], playable: [] })

/**
 * Favorite games for the current user, split into done / pending, plus the
 * pending ones the ScoreBridge extension can capture ("playable").
 *
 * - The favorites summary is shown as soon as /games returns; the extension
 *   checks run afterwards, in parallel (`checkingPlayable` is true meanwhile).
 * - Out-of-order responses are dropped (useLatestRequest).
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
  const startRequest = useLatestRequest()

  const load = useCallback(async () => {
    const isCurrent = startRequest()
    const showFavorites = ({ favorites, isError = false }) => {
      setState(favorites)
      setError(isError)
      setLoading(false)
    }

    if (userId == null) {
      showFavorites({ favorites: EMPTY })
      setCheckingPlayable(false)
      return
    }

    let favorites
    try {
      favorites = splitFavorites(await getGames())
    } catch {
      if (!isCurrent()) return
      showFavorites({ favorites: EMPTY, isError: true })
      setCheckingPlayable(false)
      return
    }
    if (!isCurrent()) return
    showFavorites({ favorites })

    const candidates = favorites.pendingGames.filter(game => game.url)
    setCheckingPlayable(candidates.length > 0)
    if (candidates.length === 0) return

    const playable = await findPlayable(candidates)
    if (!isCurrent()) return
    setState(previous => ({ ...previous, playable }))
    setCheckingPlayable(false)
  }, [userId, startRequest])

  useEffect(() => {
    if (!enabled) return undefined
    load()
    const dropInFlightResponses = () => { startRequest() }
    return dropInFlightResponses
  }, [enabled, load, startRequest])

  useEffect(() => {
    if (!enabled || !subscribe || userId == null) return undefined
    return onFavoriteChange(change => {
      if (change.optimistic) return
      load()
    })
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

function splitFavorites(games) {
  const favorites = games.filter(game => game.isFavorite)
  const pendingGames = favorites.filter(game => !game.hasSubmittedForLatest)
  return {
    total: favorites.length,
    submittedCount: favorites.length - pendingGames.length,
    pendingGames,
    playable: [],
  }
}

/** The games the extension can capture, by id; a failed check counts as "no". */
async function findPlayable(games) {
  const checks = await Promise.all(games.map(game => hasAdapterForUrl(game.url).then(Boolean, () => false)))
  return games
    .filter((_, index) => checks[index])
    .sort((a, b) => Number(a.id) - Number(b.id))
}
