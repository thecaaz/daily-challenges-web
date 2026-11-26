import { useState, useEffect, useCallback } from 'react'
import api from '../api'
import { getGames } from '../api/games'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import useAsyncData from './useAsyncData'
import useLatestRequest from './useLatestRequest'

const NO_GAMES = []

/**
 * Game/day selection and leaderboard data for a league's leaderboard tab.
 *
 * An empty selected day means "the selected game's current scoring day",
 * which the backend uses when no day is sent. A game's default day is its own
 * current scoring day (it depends on the game's reset time), not the browser's
 * calendar date.
 *
 * The leaderboard is (re)loaded while `isVisible` and a game is selected,
 * whenever the game or day changes.
 */
export default function useLeagueLeaderboard(leagueId, isVisible) {
  const { showSnackbar } = useSnackbar()
  const startRequest = useLatestRequest()
  const { data: loadedGames } = useAsyncData(getGames, [])
  const games = loadedGames ?? NO_GAMES
  const [selectedGameId, setSelectedGameId] = useState('')
  const [selectedDay, setSelectedDay] = useState('')
  const [leaderboard, setLeaderboard] = useState(null)
  const [loading, setLoading] = useState(false)

  const fetchLeaderboard = useCallback(async () => {
    const isLatest = startRequest()
    setLoading(true)
    try {
      const params = { gameId: selectedGameId }
      if (selectedDay) params.scoringDay = selectedDay
      const res = await api.get(`/leagues/${leagueId}/leaderboard`, { params })
      if (isLatest()) setLeaderboard(res.data)
    } catch (err) {
      if (!isLatest()) return
      setLeaderboard(null)
      showSnackbar(getApiErrorMessage(err, 'Failed to load leaderboard.'), 'error')
    } finally {
      if (isLatest()) setLoading(false)
    }
  }, [leagueId, selectedGameId, selectedDay, showSnackbar, startRequest])

  useEffect(() => {
    if (isVisible && selectedGameId) fetchLeaderboard()
  }, [isVisible, selectedGameId, fetchLeaderboard])

  const currentScoringDayOf = gameId =>
    games.find(game => String(game.id) === String(gameId))?.currentScoringDay || ''

  const selectGame = (gameId, day = null) => {
    setSelectedGameId(gameId)
    setSelectedDay(day ?? currentScoringDayOf(gameId))
  }

  const changeGame = gameId => {
    setSelectedGameId(gameId)
    if (!selectedDay) setSelectedDay(currentScoringDayOf(gameId))
  }

  return {
    games,
    selectedGameId,
    displayedDay: selectedDay || leaderboard?.scoringDay || '',
    leaderboard,
    loading,
    selectGame,
    changeGame,
    changeDay: setSelectedDay,
  }
}
