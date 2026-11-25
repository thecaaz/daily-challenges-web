import React from 'react'
import {
  Box, Typography, Button, Alert, Grid, Skeleton, CircularProgress,
  ToggleButtonGroup, ToggleButton,
} from '@mui/material'
import LeagueGameCard from './LeagueGameCard'

const PERIOD_DAY_OPTIONS = [7, 14, 30]
const SKELETON_CARD_COUNT = 3

function playedGamesLabel(totalCount) {
  if (totalCount <= 0) return ''
  return `${totalCount} game${totalCount !== 1 ? 's' : ''} played`
}

/**
 * Games played in the league over a selectable period.
 * `summaries` is the result of `useLeagueGameSummaries`.
 */
export default function LeagueGamesTab({ summaries, onSelectGame }) {
  return (
    <>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="body2" color="text.secondary">
          {playedGamesLabel(summaries.totalCount)}
        </Typography>
        <PeriodToggle days={summaries.days} onChange={summaries.setDays} />
      </Box>
      <GameSummaries summaries={summaries} onSelectGame={onSelectGame} />
    </>
  )
}

function PeriodToggle({ days, onChange }) {
  const handleChange = (_, selectedDays) => {
    if (selectedDays != null) onChange(selectedDays)
  }

  return (
    <ToggleButtonGroup size="small" value={days} exclusive onChange={handleChange}>
      {PERIOD_DAY_OPTIONS.map(option => (
        <ToggleButton key={option} value={option}>{option}d</ToggleButton>
      ))}
    </ToggleButtonGroup>
  )
}

function GameSummaries({ summaries, onSelectGame }) {
  const { items, totalCount, loading, error, days, loadMore, refresh } = summaries
  const hasNoGames = items.length === 0
  const hasMoreGames = items.length < totalCount

  if (loading && hasNoGames) return <GameCardSkeletons />
  if (hasNoGames && error) {
    return (
      <Alert severity="error" action={<Button color="inherit" size="small" onClick={refresh}>Retry</Button>}>
        Failed to load games.
      </Alert>
    )
  }
  if (hasNoGames) {
    return <Alert severity="info">No games played in this league in the last {days} days.</Alert>
  }

  return (
    <>
      <Grid container spacing={2}>
        {items.map(summary => (
          <Grid item xs={12} sm={6} md={4} key={summary.gameId}>
            <LeagueGameCard summary={summary} onSelect={onSelectGame} />
          </Grid>
        ))}
      </Grid>
      {error && (
        <Alert
          severity="error"
          sx={{ mt: 2 }}
          action={<Button color="inherit" size="small" onClick={loadMore} disabled={loading}>Retry</Button>}
        >
          Failed to load more games.
        </Alert>
      )}
      {hasMoreGames && (
        <Box sx={{ textAlign: 'center', mt: 2 }}>
          <Button onClick={loadMore} disabled={loading}>
            {loading ? <CircularProgress size={20} /> : 'Load more'}
          </Button>
        </Box>
      )}
    </>
  )
}

function GameCardSkeletons() {
  return (
    <Grid container spacing={2}>
      {Array.from({ length: SKELETON_CARD_COUNT }, (_, index) => (
        <Grid item xs={12} sm={6} md={4} key={index}>
          <Skeleton variant="rounded" height={200} />
        </Grid>
      ))}
    </Grid>
  )
}
