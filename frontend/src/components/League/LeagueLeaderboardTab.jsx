import React from 'react'
import {
  Box, CircularProgress, Alert, TextField, Select, MenuItem, FormControl, InputLabel,
} from '@mui/material'
import LeagueLeaderboardTable from './LeagueLeaderboardTable'

/**
 * Game/date pickers and the league leaderboard for that selection.
 * `leaderboardState` is the result of `useLeagueLeaderboard`.
 */
export default function LeagueLeaderboardTab({ leaderboardState, currentUserId }) {
  const { leaderboard, loading, selectedGameId } = leaderboardState
  const isGameUnselected = !loading && !selectedGameId
  const isLeaderboardShown = leaderboard && !loading
  const hasEntries = (leaderboard?.entries || []).length > 0

  return (
    <>
      <LeaderboardFilters leaderboardState={leaderboardState} />

      {loading && <CircularProgress size={24} sx={{ display: 'block', mx: 'auto', my: 2 }} />}
      {isGameUnselected && (
        <Alert severity="info">Select a game above, or pick one from the Games tab.</Alert>
      )}

      {isLeaderboardShown && leaderboard.scoresHidden && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Scores for this day are hidden until you submit your own.
        </Alert>
      )}

      {isLeaderboardShown && !hasEntries && (
        <Alert severity="info">No submissions from league members for this game and date.</Alert>
      )}
      {isLeaderboardShown && hasEntries && (
        <LeagueLeaderboardTable leaderboard={leaderboard} currentUserId={currentUserId} />
      )}
    </>
  )
}

function LeaderboardFilters({ leaderboardState }) {
  const { games, selectedGameId, displayedDay, changeGame, changeDay } = leaderboardState

  return (
    <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
      <FormControl size="small" sx={{ minWidth: 180 }}>
        <InputLabel>Game</InputLabel>
        <Select value={selectedGameId} label="Game" onChange={e => changeGame(e.target.value)}>
          {games.map(game => (
            <MenuItem key={game.id} value={game.id}>{game.name}</MenuItem>
          ))}
        </Select>
      </FormControl>
      <TextField
        type="date"
        size="small"
        label="Date"
        value={displayedDay}
        onChange={e => changeDay(e.target.value)}
        InputLabelProps={{ shrink: true }}
      />
    </Box>
  )
}
