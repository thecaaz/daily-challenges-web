import React from 'react'
import { Typography } from '@mui/material'
import AppButton from './AppButton'

export default function HiddenScoresCard({ gameId, search = '' }) {
  return (
    <div className="card" style={{ padding: 24 }}>
      <Typography variant="h6">Today's scores are hidden.</Typography>
      <div className="muted" style={{ marginTop: 8 }}>Submit your score to view the leaderboard for today.</div>
      <div style={{ marginTop: 12 }}>
        <AppButton to={`/submit/${gameId}${search}`}>Submit Score</AppButton>
      </div>
    </div>
  )
}
