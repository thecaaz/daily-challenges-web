import { useEffect, useState } from 'react'
import { useParams, useLocation, useSearchParams, useNavigate } from 'react-router-dom'
import { Stack, Box, Fab, Tooltip } from '@mui/material'
import CompareArrowsIcon from '@mui/icons-material/CompareArrows'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import AppButton from '../components/ui/AppButton'
import DatePickerButton from '../components/DatePickerButton'
import api from '../api'
import SubmissionCard from '../components/SubmissionCard'
import SubmissionGrid from '../components/ui/SubmissionGrid/SubmissionGrid'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'
import useGame from '../hooks/useGame'
import GameHeader from '../components/GameHeader'

export default function GameSubmissions() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
  const [submissions, setSubmissions] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(50)
  const [hasMore, setHasMore] = useState(true)
  const [selectedDate, setSelectedDate] = useState(null)
  const [availableDates, setAvailableDates] = useState([])
  const [hasSubmittedForLatest, setHasSubmittedForLatest] = useState(false)
  const [compareMode, setCompareMode] = useState(false)
  const [compareSelection, setCompareSelection] = useState([])

  const { game: hookGame, availableDates: hookAvailableDates, hasSubmittedForLatest: hookHasSubmittedForLatest, notFound: hookNotFound } = useGame(gameId)

  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()

  const toggleCompareMode = () => {
    setCompareMode(prev => !prev)
    setCompareSelection([])
  }

  const handleToggleCompare = (submission) => {
    setCompareSelection(prev => {
      const exists = prev.find(s => s.id === submission.id)
      if (exists) return prev.filter(s => s.id !== submission.id)
      if (prev.length >= 2) return [prev[1], submission]
      return [...prev, submission]
    })
  }

  const goToCompare = () => {
    if (compareSelection.length === 2) {
      navigate(`/compare/${compareSelection[0].id}/${compareSelection[1].id}`)
    }
  }

  // Sync overview data from the hook into local state and run the submissions load
  useEffect(() => {
    if (hookNotFound) {
      setNotFound(true)
      return
    }
    if (!hookGame) return

    setGame(hookGame)
    setAvailableDates(hookAvailableDates || [])
    setHasSubmittedForLatest(!!hookHasSubmittedForLatest)

    // Load submissions now that overview data is available
    fetchData()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hookGame, hookAvailableDates, hookHasSubmittedForLatest, hookNotFound])

  // respond to browser back/forward changes to the scoringDay query param
  useEffect(() => {
    const paramDay = searchParams.get('scoringDay')
    // If the param is absent (null), default to the current scoring day only when
    // the selection hasn't been initialized yet (selectedDate === null).
    if (paramDay === null) {
      if (selectedDate === null) {
        const currentDay = game?.currentScoringDay ?? hookGame?.currentScoringDay ?? ''
        if (currentDay) {
          // select the current scoring day (even if it has no submissions)
          // do not modify the URL (keep it absent)
          handleDateChange(currentDay, false)
        }
      }
      return
    }

    if (paramDay !== selectedDate) {
      // if param changed externally (history navigation), load that day
      handleDateChange(paramDay)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams, selectedDate, game?.currentScoringDay, hookGame, availableDates])

  const buildSubmissionsUrl = (pageNum, scoringDay) => {
    let url = `/submissions/game/${gameId}?page=${pageNum}&pageSize=${pageSize}`
    if (scoringDay) url += `&scoringDay=${encodeURIComponent(scoringDay)}`
    return url
  }

  const fetchSubmissionsPage = async (pageNum, scoringDay) => {
    const res = await api.get(buildSubmissionsUrl(pageNum, scoringDay))
    return res.data || { items: [], hasSubmittedForLatest: false, hasMore: false }
  }

  const fetchData = async () => {
    // Use hook-provided overview when available (hookGame/hookAvailableDates/hookHasSubmittedForLatest)
    try {
      const gameData = hookGame || game
      if (!gameData) return

      const dates = (hookAvailableDates && hookAvailableDates.length) ? hookAvailableDates : availableDates || []

      const preferred = searchParams.get('scoringDay') || location?.state?.selectedDate
      const currentDay = gameData?.currentScoringDay ?? ''
      let initialSelected = ''
      if (preferred && dates.includes(preferred)) initialSelected = preferred
      else if (currentDay) initialSelected = currentDay
      else initialSelected = ''

      setSelectedDate(initialSelected)
      setPage(1)

      const userHasSubmitted = !!hookHasSubmittedForLatest
      setHasSubmittedForLatest(userHasSubmitted)

      if (initialSelected) {
        const dayPage = await fetchSubmissionsPage(1, initialSelected)
        const subs = dayPage.items || []
        setSubmissions(subs)
        if (typeof dayPage.totalPages === 'number') setHasMore(dayPage.page < dayPage.totalPages)
        else setHasMore(dayPage.hasMore === true)
      } else {
        if (!userHasSubmitted) {
          setSubmissions([])
          setHasMore(false)
        } else {
          const initial = await fetchSubmissionsPage(1)
          const initialSubs = initial.items || []
          if (typeof initial.totalPages === 'number') setHasMore(initial.page < initial.totalPages)
          else setHasMore(initial.hasMore === true)
          setSubmissions(initialSubs)
        }
      }
    } catch (err) {
      const e = err
      if (e?.response?.status === 404) {
        setNotFound(true)
        return
      }
      throw err
    }
  }

  const loadMore = async () => {
    const next = page + 1
    const pageResult = await fetchSubmissionsPage(next, selectedDate || undefined)
    const more = pageResult.items || []
    setSubmissions(prev => [...prev, ...more])
    setPage(next)
    if (typeof pageResult.totalPages === 'number') setHasMore(pageResult.page < pageResult.totalPages)
    else setHasMore(pageResult.hasMore === true)
  }

  const handleDateChange = async (value, updateUrl = true) => {
    // update url param (only when requested)
    if (updateUrl) {
      if (value) setSearchParams({ scoringDay: value })
      else setSearchParams({})
    }

    setSelectedDate(value)
    setPage(1)
    setSubmissions([])
    // If clearing selection (viewing latest) and the user hasn't submitted, skip fetching list
    if (!value && !hasSubmittedForLatest) {
      setHasMore(false)
      return
    }

    const pageResult = await fetchSubmissionsPage(1, value || undefined)
    const subs = pageResult.items || []
    setSubmissions(subs)
    if (typeof pageResult.totalPages === 'number') setHasMore(pageResult.page < pageResult.totalPages)
    else setHasMore(pageResult.hasMore === true)
    // availableDates is provided by the dedicated endpoint; do not override here.
  }


  const filtered = selectedDate ? submissions.filter(s => s.scoringDay === selectedDate) : submissions

  // Determine whether we're viewing the latest day (most recent scoring day)
  const currentScoringDay = game?.currentScoringDay ?? ''
  const isViewingLatest = !selectedDate || selectedDate === currentScoringDay

  if (notFound) return <NotFound message="Game not found" />
  if (!game) return <Loading />

  return (
    <div>
      <GameHeader game={game} />

      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <DatePickerButton availableDates={availableDates} selectedDate={selectedDate} onChange={handleDateChange} />
        </Box>
        <div>
          {filtered.filter(s => s.screenshotUrl).length >= 2 && (
            <AppButton
              onClick={toggleCompareMode}
              sx={{ mr: 1, ...(compareMode ? {} : { background: 'white', color: '#444', boxShadow: 'none' }) }}
            >
              {compareMode ? 'Cancel Compare' : 'Compare'}
            </AppButton>
          )}
          <AppButton to="/" sx={{ mr: 1, background: 'white', color: '#444', boxShadow: 'none' }}>Back</AppButton>
          {(() => {
            const submitDisabled = isViewingLatest && hasSubmittedForLatest
              return (
              <AppButton
                component={submitDisabled ? 'span' : undefined}
                to={submitDisabled ? undefined : `/submit/${game.id}${location.search || ''}`}
                disabled={submitDisabled}
                title={submitDisabled ? "You've already submitted for today" : undefined}
                variant="contained"
                color="primary"
                size="small"
                startIcon={<EmojiEventsIcon />}
                aria-label={submitDisabled ? 'Already submitted today' : 'Submit score'}
              >
                {submitDisabled ? 'Submitted' : 'Submit Score'}
              </AppButton>
            )
          })()}
        </div>
      </Stack>

          {isViewingLatest && !hasSubmittedForLatest ? (
            <HiddenScoresCard gameId={game.id} search={location.search || ''} />
          ) : (
            <>
              <SubmissionGrid
                items={filtered}
                ItemComponent={SubmissionCard}
                containerSx={{ mt: 1 }}
                itemProps={(item) => ({
                  compareMode,
                  selected: compareSelection.some(s => s.id === item.id),
                  onToggleCompare: handleToggleCompare,
                })}
              />
            {hasMore && (
              <div style={{ marginTop: 16, textAlign: 'center' }}>
                <AppButton onClick={loadMore}>Load more</AppButton>
              </div>
            )}
            </>
          )}

      {compareMode && compareSelection.length === 2 && (
        <Tooltip title="Compare selected submissions">
          <Fab
            color="primary"
            onClick={goToCompare}
            sx={{ position: 'fixed', bottom: 24, right: 24, zIndex: 1300 }}
          >
            <CompareArrowsIcon />
          </Fab>
        </Tooltip>
      )}
    </div>
  )
}
