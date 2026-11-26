import { useState, useEffect, useCallback } from 'react'
import { getGames } from '../api/games'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * All games, for the admin page. Loads on mount; `reload()` refreshes after a
 * change. A failed load keeps the current list and shows a snackbar.
 *
 * @returns {{ games: object[], reload: () => Promise<void> }}
 */
export default function useAdminGames() {
  const { showSnackbar } = useSnackbar()
  const [games, setGames] = useState([])

  const reload = useCallback(async () => {
    try {
      setGames(await getGames())
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to load games'), 'error')
    }
  }, [showSnackbar])

  useEffect(() => { reload() }, [reload])

  return { games, reload }
}
