import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, useLocation, useSearchParams, useNavigate } from 'react-router-dom'
import { Stack, Box, Fab, Tooltip, Typography, CircularProgress } from '@mui/material'
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
import { useAuth } from '../contexts/AuthContext'
import GameHeader from '../components/GameHeader'

const PAGE_SIZE = 50
// URL value for "all days" (an absent param means "the current scoring day").
export const ALL_DAYS = 'all'

async function fetchSubmissionsPage(gameId, pageNum, scoringDay) {
  let url = `/submissions/game/${gameId}?page=${pageNum}&pageSize=${PAGE_SIZE}`
  if (scoringDay) url += `&scoringDay=${encodeURIComponent(scoringDay)}`
  const res = await api.get(url)
  return res?.data || { items: [], hasMore: false }
}

// Append a page, skipping items already shown (rows can shift between pages
// when new submissions arrive while paging).
export function appendUniqueById(prev, items) {
  const seen = new Set(prev.map(s => s.id))
  const fresh = (items || []).filter(s => {
    if (s?.id == null) return true
    if (seen.has(s.id)) return false
    seen.add(s.id)
    return true
  })
  return fresh.length ? [...prev, ...fresh] : prev
}

function pageHasMore(pageResult) {
  if (typeof pageResult.totalPages === 'number') return pageResult.page < pageResult.totalPages
  return pageResult.hasMore === true
}

export default function GameSubmissions() {
  const { gameId } = useParams()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const { user } = useAuth() || {}
  const isAdmin = !!user?.isAdmin

  const {
    game,
    availableDates,
    hasSubmittedForLatest,
    notFound,
    error: gameError,
    refresh: refreshGame,
  } = useGame(gameId)

  const [submissions, setSubmissions] = useState([])
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(false)
  const [listLoading, setListLoading] = useState(false)
  const [listError, setListError] = useState(false)
  const [listNotFound, setListNotFound] = useState(false)
  const [scoresHidden, setScoresHidden] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [compareMode, setCompareMode] = useState(false)
  const [compareSelection, setCompareSelection] = useState([])
  // Every list request gets an id; responses for anything but the latest are dropped.
  const requestRef = useRef(0)

  // The URL is the source of truth for the selected day, so Back/Forward just work:
  //   ?scoringDay=YYYY-MM-DD -> that day
  //   ?scoringDay=all        -> all days ('')
  //   (absent)               -> the game's current scoring day
  const currentScoringDay = game?.currentScoringDay ?? ''
  const paramDay = searchParams.get('scoringDay')
  const selectedDate = paramDay === ALL_DAYS ? '' : (paramDay || currentScoringDay)

  const isViewingLatest = !!currentScoringDay && selectedDate === currentScoringDay
  // Today's scores stay hidden until the user has submitted: don't even ask for
  // them. Admins may always see them (the backend allows it).
  const hideLatest = isViewingLatest && !hasSubmittedForLatest && !isAdmin
  const hasGame = !!game

  useEffect(() => {
    if (!hasGame) return
    const requestId = ++requestRef.current
    setSubmissions([])
    setPage(1)
    setHasMore(false)
    setListError(false)
    setListNotFound(false)
    setScoresHidden(false)
    setCompareSelection([])

    if (hideLatest) {
      setListLoading(false)
      return
    }

    setListLoading(true)
    fetchSubmissionsPage(gameId, 1, selectedDate || undefined)
      .then(pageResult => {
        if (requestId !== requestRef.current) return
        setSubmissions(pageResult.items || [])
        setHasMore(pageHasMore(pageResult))
        setScoresHidden(!!pageResult.scoresHidden)
      })
      .catch(err => {
        if (requestId !== requestRef.current) return
        if (err?.response?.status === 404) setListNotFound(true)
        else setListError(true)
      })
      .finally(() => {
        if (requestId === requestRef.current) setListLoading(false)
      })
  }, [gameId, hasGame, selectedDate, hideLatest, reloadKey])

  const loadMore = useCallback(async () => {
    if (listLoading) return
    const requestId = ++requestRef.current
    const next = page + 1
    setListLoading(true)
    try {
      const pageResult = await fetchSubmissionsPage(gameId, next, selectedDate || undefined)
      if (requestId !== requestRef.current) return
      setSubmissions(prev => appendUniqueById(prev, pageResult.items))
      setPage(next)
      setHasMore(pageHasMore(pageResult))
      if (pageResult.scoresHidden) setScoresHidden(true)
    } catch {
      if (requestId !== requestRef.current) return
      setListError(true)
    } finally {
      if (requestId === requestRef.current) setListLoading(false)
    }
  }, [gameId, listLoading, page, selectedDate])

  const handleDateChange = (value) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      next.set('scoringDay', value || ALL_DAYS)
      return next
    })
  }

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

  if (notFound || listNotFound) return <NotFound message="Game not found" />
  if (!game) {
    if (gameError) {
      return (
        <Box sx={{ textAlign: 'center', py: 6 }}>
          <Typography gutterBottom>Failed to load this game.</Typography>
          <AppButton onClick={refreshGame}>Retry</AppButton>
        </Box>
      )
    }
    return <Loading />
  }

  const submitDisabled = isViewingLatest && hasSubmittedForLatest
  const comparable = !scoresHidden && submissions.filter(s => s.screenshotUrl).length >= 2

  return (
    <div>
      <GameHeader game={game} onExpire={refreshGame} />

      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <DatePickerButton availableDates={availableDates} selectedDate={selectedDate} onChange={handleDateChange} />
        </Box>
        <div>
          {comparable && (
            <AppButton
              onClick={toggleCompareMode}
              sx={{ mr: 1, ...(compareMode ? {} : { bgcolor: 'background.paper', color: 'text.primary', boxShadow: 'none' }) }}
            >
              {compareMode ? 'Cancel Compare' : 'Compare'}
            </AppButton>
          )}
          <AppButton to="/" sx={{ mr: 1, bgcolor: 'background.paper', color: 'text.primary', boxShadow: 'none' }}>Back</AppButton>
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
        </div>
      </Stack>

      {hideLatest ? (
        <HiddenScoresCard gameId={game.id} search={location.search || ''} />
      ) : (
        <>
          {/* "All days" leaves today out for non-submitters; masked lists come back with scoresHidden. */}
          {(scoresHidden || (!selectedDate && !hasSubmittedForLatest)) && (
            <HiddenScoresCard gameId={game.id} search={location.search || ''} />
          )}
          {listError && (
            <Box sx={{ textAlign: 'center', py: 3 }}>
              <Typography gutterBottom>Failed to load submissions.</Typography>
              <AppButton onClick={() => setReloadKey(k => k + 1)}>Retry</AppButton>
            </Box>
          )}
          {!listError && !listLoading && submissions.length === 0 && (
            <Typography color="text.secondary" sx={{ mt: 2 }}>No submissions for this day yet.</Typography>
          )}
          <SubmissionGrid
            items={submissions}
            ItemComponent={SubmissionCard}
            containerSx={{ mt: 1 }}
            itemProps={(item) => ({
              compareMode,
              selected: compareSelection.some(s => s.id === item.id),
              onToggleCompare: handleToggleCompare,
            })}
          />
          {listLoading && submissions.length === 0 && (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress size={28} /></Box>
          )}
          {hasMore && (
            <div style={{ marginTop: 16, textAlign: 'center' }}>
              <AppButton onClick={loadMore} disabled={listLoading}>
                {listLoading ? 'Loading…' : 'Load more'}
              </AppButton>
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
