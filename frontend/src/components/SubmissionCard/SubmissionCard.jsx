import React from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Card, CardContent, CardMedia, Typography, Checkbox } from '@mui/material'
import imageUrl from '../../utils/imageUrl'
import { formatDateTime } from '../../utils/dateFormat'
import { isScoreHidden } from './isScoreHidden'
import stopCardNavigation from './stopCardNavigation'
import SubmissionBadges from './SubmissionBadges'
import ScoreValue from './ScoreValue'

const SCREENSHOT_HEIGHT = 160

const selectedOutlineSx = { outline: '3px solid', outlineColor: 'primary.main', outlineOffset: -3 }
const compareCursorSx = { cursor: 'pointer' }
const compareCheckboxSx = { position: 'absolute', top: 4, right: 4, zIndex: 1 }
const screenshotSx = { objectFit: 'cover' }
const linkResetStyle = { textDecoration: 'none', color: 'inherit', display: 'block' }
const unlinkedStyle = { display: 'block' }

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

  const card = (
    <Card sx={cardSxFor({ compareMode, selected })} onClick={compareMode ? handleCompareClick : undefined}>
      {canSelectForCompare && (
        <Checkbox checked={!!selected} sx={compareCheckboxSx} onClick={handleCompareClick} />
      )}
      {hasVisibleScreenshot && (
        <CardMedia
          component="img"
          image={imageUrl(submission.screenshotUrl)}
          alt="screenshot"
          height={SCREENSHOT_HEIGHT}
          loading="lazy"
          sx={screenshotSx}
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

  if (!isLinkedToDetail) return <div style={unlinkedStyle}>{card}</div>

  return (
    <Link to={`/submission/${submission.id}${location.search || ''}`} style={linkResetStyle}>
      {card}
    </Link>
  )
}

function cardSxFor({ compareMode, selected }) {
  return {
    height: '100%',
    position: 'relative',
    ...(compareMode && selected ? selectedOutlineSx : {}),
    ...(compareMode ? compareCursorSx : {}),
  }
}
