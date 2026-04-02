import React from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Card, CardContent, CardMedia, Typography, Tooltip, Box, Chip, Checkbox } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import PersonIcon from '@mui/icons-material/Person'
import imageUrl from '../utils/imageUrl'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import { useAuth } from '../contexts/AuthContext'

export default function SubmissionCard({ submission, compareMode, selected, onToggleCompare }) {
  if (!submission) return null
  const { user } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()

  const handleCompareClick = (e) => {
    e.preventDefault()
    e.stopPropagation()
    if (onToggleCompare) onToggleCompare(submission)
  }

  const cardContent = (
    <Card sx={{
      height: '100%',
      position: 'relative',
      ...(compareMode && selected ? { outline: '3px solid', outlineColor: 'primary.main', outlineOffset: -3 } : {}),
      ...(compareMode ? { cursor: 'pointer' } : {}),
    }}
    onClick={compareMode ? handleCompareClick : undefined}
    >
        {compareMode && (
          <Checkbox
            checked={!!selected}
            sx={{ position: 'absolute', top: 4, right: 4, zIndex: 1 }}
            onClick={handleCompareClick}
          />
        )}
        {submission.screenshotUrl && (
          <CardMedia
            component="img"
            image={imageUrl(submission.screenshotUrl)}
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
              {submission.userId ? (
                <Typography
                  component="span"
                  variant="subtitle1"
                  sx={{ fontWeight: 700, lineHeight: 1.2, cursor: 'pointer', color: 'primary.main', textDecoration: 'underline' }}
                  onClick={(e) => { e.preventDefault(); e.stopPropagation(); navigate(`/users/${submission.userId}`) }}
                  onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); navigate(`/users/${submission.userId}`) } }}
                  tabIndex={0}
                  role="link"
                >
                  {submission.username ?? 'Anonymous'}
                </Typography>
              ) : (
                <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
                  {submission.username ?? 'Anonymous'}
                </Typography>
              )}
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
            {formatNumber(submission.score)}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {formatDateTime(submission.createdAt)}
          </Typography>
        </CardContent>
      </Card>
  )

  if (compareMode) {
    return <div style={{ display: 'block' }}>{cardContent}</div>
  }

  return (
    <Link to={`/submission/${submission.id}${location.search || ''}`} style={{ textDecoration: 'none', color: 'inherit', display: 'block' }}>
      {cardContent}
    </Link>
  )
}
