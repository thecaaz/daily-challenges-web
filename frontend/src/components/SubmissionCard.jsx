import React from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Card, CardContent, CardMedia, Typography, Tooltip, Box, Chip } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import PersonIcon from '@mui/icons-material/Person'
import api, { getApiRoot } from '../api'
import parseUtcDate from '../utils/parseUtcDate'
import { useAuth } from '../contexts/AuthContext'

export default function SubmissionCard({ submission }) {
  if (!submission) return null
  const { user } = useAuth()
  const apiRoot = getApiRoot()
  const location = useLocation()

  return (
    <Link to={`/submission/${submission.id}${location.search || ''}`} style={{ textDecoration: 'none', color: 'inherit', display: 'block' }}>
      <Card sx={{ height: '100%' }}>
        {submission.screenshotUrl && (
          <CardMedia
            component="img"
            image={`${apiRoot}${submission.screenshotUrl}`}
            alt="screenshot"
            height={160}
            loading="lazy"
            sx={{ objectFit: 'cover' }}
          />
        )}
        <CardContent>
          {/* Rank badge + winner icon */}
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 0.5 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap' }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
                {submission.username ?? 'Anonymous'}
              </Typography>
              {submission.isDayWinner && (
                <Tooltip title={submission.scoringDay ? `Winner for ${submission.scoringDay}` : 'Winner'}>
                  <EmojiEventsIcon sx={{ color: '#FFD700', fontSize: '1.1rem' }} />
                </Tooltip>
              )}
              {submission.userId && user && submission.userId === user.id && (
                <Chip
                  icon={<PersonIcon sx={{ fontSize: '0.8rem !important' }} />}
                  label="You"
                  size="small"
                  color="primary"
                  sx={{ height: 18, fontSize: '0.65rem', fontWeight: 700 }}
                />
              )}
            </Box>
            <Chip
              label={`#${submission.rank ?? '—'}`}
              size="small"
              color="secondary"
              sx={{ fontWeight: 700, fontSize: '0.7rem' }}
            />
          </Box>

          <Typography variant="h6" sx={{ fontWeight: 800, color: 'text.primary', lineHeight: 1 }}>
            {submission.score}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {parseUtcDate(submission.createdAt).toLocaleString()}
          </Typography>
        </CardContent>
      </Card>
    </Link>
  )
}
