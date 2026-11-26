import React from 'react'
import useSubmissionPair from '../hooks/useSubmissionPair'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Typography, Card, Box, Chip, Tooltip, Divider } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import AppButton from '../components/ui/AppButton'
import PanZoomImage from '../components/ui/PanZoomImage'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import imageUrl from '../utils/imageUrl'
import goBackOrRoute from '../utils/navigation'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import { isScoreHidden } from '../components/SubmissionCard'
import { winnerGold } from '../styles/tokens'
import OptionalUserLink from '../components/ui/OptionalUserLink'

const winnerIconSx = { color: winnerGold, fontSize: '1.2rem' }

function SubmitterName({ submission }) {
  return (
    <OptionalUserLink userId={submission.userId}>
      <Typography variant="h6" sx={{ fontWeight: 700 }}>{submission.username}</Typography>
    </OptionalUserLink>
  )
}

function SubmissionPanel({ submission }) {
  const masked = isScoreHidden(submission)
  const winnerTitle = submission.scoringDay ? `Winner for ${submission.scoringDay}` : 'Winner'
  const scoreText = masked ? 'Hidden' : formatNumber(submission.score)
  const missingScreenshotText = masked ? 'Screenshot hidden until you submit today' : 'No screenshot attached'
  return (
    <Box sx={{ flex: 1, minWidth: 0 }}>
      <Box sx={{ mb: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          <SubmitterName submission={submission} />
          {submission.isDayWinner && (
            <Tooltip title={winnerTitle}>
              <EmojiEventsIcon sx={winnerIconSx} />
            </Tooltip>
          )}
          {submission.rank != null && (
            <Chip label={`#${submission.rank}`} size="small" color="secondary" sx={{ fontWeight: 700, fontSize: '0.75rem' }} />
          )}
        </Box>
        <Typography variant="h5" sx={{ fontWeight: 800, lineHeight: 1.2 }}>{scoreText}</Typography>
        <Typography variant="caption" color="text.secondary">
          {formatDateTime(submission.createdAt)}
          {submission.scoringDay && ` · Day: ${submission.scoringDay}`}
        </Typography>
      </Box>
      {submission.screenshotUrl ? (
        <PanZoomImage src={imageUrl(submission.screenshotUrl)} alt={`${submission.username ?? 'Anonymous'} screenshot`} />
      ) : (
        <Card sx={{ p: 3, textAlign: 'center', height: 500, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Typography color="text.secondary">{missingScreenshotText}</Typography>
        </Card>
      )}
    </Box>
  )
}

export default function CompareSubmissions() {
  const { id1, id2 } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const { first, second, loading, error } = useSubmissionPair(id1, id2)

  if (loading) return <Loading />
  if (error || !first || !second) return <NotFound message="Could not load submissions for comparison" />

  const goBack = () => goBackOrRoute(navigate, location, first.gameId ? `/games/${first.gameId}` : '/', {})

  return (
    <div>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Compare Submissions</Typography>
        <AppButton onClick={goBack}>Back</AppButton>
      </Box>

      <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
        <SubmissionPanel submission={first} />
        <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', md: 'block' } }} />
        <Divider sx={{ display: { xs: 'block', md: 'none' } }} />
        <SubmissionPanel submission={second} />
      </Box>
    </div>
  )
}
