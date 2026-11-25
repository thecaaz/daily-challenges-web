import React from 'react'
import { useParams, useLocation } from 'react-router-dom'
import { Box, Fab, Tooltip, Typography } from '@mui/material'
import CompareArrowsIcon from '@mui/icons-material/CompareArrows'
import AppButton from '../components/ui/AppButton'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'
import GameHeader from '../components/GameHeader'
import GameSubmissionsToolbar from '../components/GameSubmissions/GameSubmissionsToolbar'
import GameSubmissionsList from '../components/GameSubmissions/GameSubmissionsList'
import useGame from '../hooks/useGame'
import useSelectedScoringDay from '../hooks/useSelectedScoringDay'
import useSubmissionPages from '../hooks/useSubmissionPages'
import useCompareSelection from '../hooks/useCompareSelection'
import { useAuth } from '../contexts/AuthContext'

const MIN_SCREENSHOTS_TO_COMPARE = 2

/**
 * Today's scores stay hidden until the user has submitted, so today's list is
 * not even requested for them. Admins may always see it (the backend allows it).
 * "All days" leaves today out for non-submitters; masked lists come back with
 * `scoresHidden`.
 */
export default function GameSubmissions() {
  const { gameId } = useParams()
  const location = useLocation()
  const { user } = useAuth() || {}
  const isAdmin = !!user?.isAdmin

  const { game, availableDates, hasSubmittedForLatest, notFound, error: gameError, refresh: refreshGame } = useGame(gameId)
  const currentScoringDay = game?.currentScoringDay ?? ''
  const { selectedDate, selectDate } = useSelectedScoringDay(currentScoringDay)
  const isViewingLatest = !!currentScoringDay && selectedDate === currentScoringDay
  const isLatestHidden = isViewingLatest && !hasSubmittedForLatest && !isAdmin
  const list = useSubmissionPages({ gameId, scoringDay: selectedDate, enabled: !!game && !isLatestHidden })
  const compare = useCompareSelection(list.listKey)

  if (notFound || list.isNotFound) return <NotFound message="Game not found" />
  if (!game) return gameError ? <GameLoadError onRetry={refreshGame} /> : <Loading />

  const hasSubmittedForSelectedDay = isViewingLatest && hasSubmittedForLatest
  const screenshotCount = list.submissions.filter(s => s.screenshotUrl).length
  const canOfferCompare = !list.scoresHidden && screenshotCount >= MIN_SCREENSHOTS_TO_COMPARE
  const isTodayLeftOut = !selectedDate && !hasSubmittedForLatest
  const showsHiddenScoresNotice = list.scoresHidden || isTodayLeftOut
  const hiddenScoresCard = <HiddenScoresCard gameId={game.id} search={location.search || ''} />

  return (
    <div>
      <GameHeader game={game} onExpire={refreshGame} />
      <GameSubmissionsToolbar
        game={game}
        day={{ availableDates, selectedDate, selectDate }}
        compare={compare}
        canOfferCompare={canOfferCompare}
        hasSubmitted={hasSubmittedForSelectedDay}
      />
      {isLatestHidden ? hiddenScoresCard : (
        <>
          {showsHiddenScoresNotice && hiddenScoresCard}
          <GameSubmissionsList list={list} compare={compare} />
        </>
      )}
      {compare.canCompare && <CompareFab onClick={compare.compare} />}
    </div>
  )
}

function GameLoadError({ onRetry }) {
  return (
    <Box sx={{ textAlign: 'center', py: 6 }}>
      <Typography gutterBottom>Failed to load this game.</Typography>
      <AppButton onClick={onRetry}>Retry</AppButton>
    </Box>
  )
}

function CompareFab({ onClick }) {
  return (
    <Tooltip title="Compare selected submissions">
      <Fab color="primary" onClick={onClick} sx={{ position: 'fixed', bottom: 24, right: 24, zIndex: 1300 }}>
        <CompareArrowsIcon />
      </Fab>
    </Tooltip>
  )
}
