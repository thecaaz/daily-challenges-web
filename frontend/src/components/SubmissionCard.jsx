import React from 'react'
import { Link } from 'react-router-dom'
import { CardContent, Typography } from '@mui/material'
import api from '../api'
import { useAuth } from '../contexts/AuthContext'

export default function SubmissionCard({ submission }) {
  if (!submission) return null
  const { user } = useAuth()
  const apiRoot = api.defaults.baseURL ? api.defaults.baseURL.replace(/\/api\/?$/, '') : 'http://localhost:5000'

  return (
    <Link to={`/submission/${submission.id}`} style={{ textDecoration: 'none', color: 'inherit' }}>
      <div className="card">
        {submission.screenshotUrl && <img className="game-list-image" src={`${apiRoot}${submission.screenshotUrl}`} alt="screenshot" />}
        <CardContent>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
              <Typography variant="subtitle1">{submission.username ?? 'Anonymous'}</Typography>
              {submission.userId && user && submission.userId === user.id && (
                <Typography variant="caption" sx={{ color: '#666' }}>You</Typography>
              )}
            </div>
            <div className="badge">#{submission.rank ?? ''}</div>
          </div>
          <Typography variant="h6">{submission.score}</Typography>
          <Typography variant="caption">{new Date(submission.createdAt).toLocaleString()}</Typography>
        </CardContent>
      </div>
    </Link>
  )
}
