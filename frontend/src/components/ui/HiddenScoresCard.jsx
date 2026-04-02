import React, { useState, useEffect } from 'react'
import { Typography, Card, Tooltip, CircularProgress } from '@mui/material'
import AppButton from './AppButton'
import api from '../../api'
import formatNumber from '../../utils/formatNumber'

function formatUsernames(names) {
  if (names.length === 1) return `${names[0]} has already submitted`
  if (names.length === 2) return `${names[0]} and ${names[1]} have already submitted`
  const allButLast = names.slice(0, -1).join(', ')
  return `${allButLast} and ${names[names.length - 1]} have already submitted`
}

export default function HiddenScoresCard({ gameId, search = '' }) {
  const [submitters, setSubmitters] = useState(null)

  useEffect(() => {
    api.get(`/submissions/game/${gameId}/today-submitters`)
      .then(res => setSubmitters(res.data))
      .catch(() => setSubmitters({ count: 0, usernames: [] }))
  }, [gameId])

  const renderSubmitters = () => {
    if (submitters === null) {
      return <CircularProgress size={16} sx={{ mt: 1 }} />
    }
    if (submitters.count === 0) {
      return (
        <div className="muted" style={{ marginTop: 8 }}>
          🎯 Be the first to submit your score!
        </div>
      )
    }
    if (submitters.count <= 3) {
      return (
        <div className="muted" style={{ marginTop: 8 }}>
          {formatUsernames(submitters.usernames)}
        </div>
      )
    }
    return (
      <div className="muted" style={{ marginTop: 8 }}>
        <Tooltip title={submitters.usernames.join(', ')} arrow>
            <span style={{ cursor: 'default', textDecoration: 'underline dotted' }}>
            {formatNumber(submitters.count)} players have already submitted
          </span>
        </Tooltip>
      </div>
    )
  }

  return (
    <Card sx={{ p: 3 }}>
      <Typography variant="h6">Todays scores are hidden.</Typography>
      <div className="muted" style={{ marginTop: 8 }}>Submit your score to view the leaderboard for today.</div>
      {renderSubmitters()}
      <div style={{ marginTop: 12 }}>
        <AppButton to={`/submit/${gameId}${search}`}>Submit Score</AppButton>
      </div>
    </Card>
  )
}
