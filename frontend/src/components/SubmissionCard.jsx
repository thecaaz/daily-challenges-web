import React from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Card, CardContent, CardMedia, Typography, Tooltip, Box, Chip, Checkbox } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import PersonIcon from '@mui/icons-material/Person'
import LockIcon from '@mui/icons-material/Lock'
import imageUrl from '../utils/imageUrl'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import { useAuth } from '../contexts/AuthContext'
import { winnerGold } from '../styles/tokens'

/**
 * The server masks today's scores while the viewer hasn't submitted yet:
 * score, rank and screenshot are then null or missing.
 */
export function isScoreHidden(submission) {
  return submission.score == null || submission.score === ''
}

const selectedOutlineSx = { outline: '3px solid', outlineColor: 'primary.main', outlineOffset: -3 }
const linkResetStyle = { textDecoration: 'none', color: 'inherit', display: 'block' }

function stopCardNavigation(event) {
  event.preventDefault()
  event.stopPropagation()
}

/** Submitter name; links to the profile without triggering the card's own link. */
function SubmitterName({ submission }) {
  const navigate = useNavigate()
  const name = submission.username ?? 'Anonymous'

  if (!submission.userId) {
    return <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.2 }}>{name}</Typography>
  }

  const openProfile = event => {
    stopCardNavigation(event)
    navigate(`/users/${submission.userId}`)
  }
  const handleKeyDown = event => {
    if (event.key === 'Enter' || event.key === ' ') openProfile(event)
  }

  return (
    <Typography
      component="span"
      variant="subtitle1"
      sx={{ fontWeight: 700, lineHeight: 1.2, cursor: 'pointer', color: 'primary.main', textDecoration: 'underline' }}
      onClick={openProfile}
      onKeyDown={handleKeyDown}
      tabIndex={0}
      role="link"
    >
      {name}
    </Typography>
  )
}

function SubmissionBadges({ submission }) {
  const { user } = useAuth()
  const isOwnSubmission = Boolean(submission.userId && user && submission.userId === user.id)
  const winnerTitle = submission.scoringDay ? `Winner for ${submission.scoringDay}` : 'Winner'

  return (
    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 0.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap' }}>
        <SubmitterName submission={submission} />
        {submission.isDayWinner && (
          <Tooltip title={winnerTitle}>
            <EmojiEventsIcon sx={{ color: winnerGold, fontSize: '1.1rem' }} />
          </Tooltip>
        )}
        {isOwnSubmission && (
          <Chip
            icon={<PersonIcon sx={{ fontSize: '0.8rem !important' }} />}
            label="You"
            size="small"
            color="primary"
            sx={{ height: 18, fontSize: '0.65rem', fontWeight: 700 }}
          />
        )}
      </Box>
      {submission.rank != null && (
        <Chip label={`#${submission.rank}`} size="small" color="secondary" sx={{ fontWeight: 700, fontSize: '0.7rem' }} />
      )}
    </Box>
  )
}

function ScoreValue({ submission, masked }) {
  if (masked) {
    return (
      <Typography variant="h6" color="text.secondary" sx={{ fontWeight: 700, lineHeight: 1, display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <LockIcon fontSize="small" /> Hidden
      </Typography>
    )
  }
  return (
    <Typography variant="h6" sx={{ fontWeight: 800, color: 'text.primary', lineHeight: 1 }}>
      {formatNumber(submission.score)}
    </Typography>
  )
}

export default function SubmissionCard({ submission, compareMode, selected, onToggleCompare }) {
  const location = useLocation()
  if (!submission) return null

  const masked = isScoreHidden(submission)
  const canSelectForCompare = compareMode && !masked
  const hasVisibleScreenshot = !masked && Boolean(submission.screenshotUrl)
  const isLinkedToDetail = !compareMode && !masked

  const handleCompareClick = event => {
    stopCardNavigation(event)
    if (onToggleCompare) onToggleCompare(submission)
  }

  const cardSx = {
    height: '100%',
    position: 'relative',
    ...(compareMode && selected ? selectedOutlineSx : {}),
    ...(compareMode ? { cursor: 'pointer' } : {}),
  }

  const card = (
    <Card sx={cardSx} onClick={compareMode ? handleCompareClick : undefined}>
      {canSelectForCompare && (
        <Checkbox checked={!!selected} sx={{ position: 'absolute', top: 4, right: 4, zIndex: 1 }} onClick={handleCompareClick} />
      )}
      {hasVisibleScreenshot && (
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
        <SubmissionBadges submission={submission} />
        <ScoreValue submission={submission} masked={masked} />
        <Typography variant="caption" color="text.secondary">
          {formatDateTime(submission.createdAt)}
        </Typography>
      </CardContent>
    </Card>
  )

  if (!isLinkedToDetail) return <div style={{ display: 'block' }}>{card}</div>

  return (
    <Link to={`/submission/${submission.id}${location.search || ''}`} style={linkResetStyle}>
      {card}
    </Link>
  )
}
