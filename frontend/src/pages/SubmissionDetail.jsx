import React from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Typography, Card, Box, Chip, Tooltip } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import AppButton from '../components/ui/AppButton'
import PanZoomImage from '../components/ui/PanZoomImage'
import imageUrl from '../utils/imageUrl'
import goBackOrRoute from '../utils/navigation'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import useSubmission from '../hooks/useSubmission'
import { isScoreHidden } from '../components/SubmissionCard'
import { winnerGold } from '../styles/tokens'
import OptionalUserLink from '../components/ui/OptionalUserLink'

const winnerIconSx = { color: winnerGold, fontSize: '1.1rem' }

export default function SubmissionDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const { submission, loading, error, retry } = useSubmission(id)

  if (loading) return <Loading />
  if (error) {
    return (
      <Box sx={{ textAlign: 'center', py: 6 }}>
        <Typography gutterBottom>Failed to load this submission.</Typography>
        <AppButton onClick={retry}>Retry</AppButton>
      </Box>
    )
  }
  if (!submission) return <NotFound message="Submission not found" />

  const masked = isScoreHidden(submission)
  const scoreText = masked ? 'hidden until you submit today' : formatNumber(submission.score)
  const missingScreenshotText = masked ? 'The screenshot is hidden until you submit your own score for today.' : 'No screenshot attached.'
  const goBack = () => goBackOrRoute(navigate, location, `/games/${submission.gameId}`, { locationSearch: location.search, scoringDay: submission.scoringDay })

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <Typography variant="h5">
            Submission — <OptionalUserLink userId={submission.userId}>{submission.username}</OptionalUserLink>
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <Typography variant="caption">
              Score: {scoreText} — {formatDateTime(submission.createdAt)}
              {submission.scoringDay && ` · Day: ${submission.scoringDay}`}
            </Typography>
            {submission.rank != null && (
              <Chip label={`#${submission.rank}`} size="small" color="secondary" sx={{ fontWeight: 700, fontSize: '0.7rem' }} />
            )}
            {submission.isDayWinner && (
              <Tooltip title={submission.scoringDay ? `Winner for ${submission.scoringDay}` : 'Winner'}>
                <EmojiEventsIcon sx={winnerIconSx} />
              </Tooltip>
            )}
          </Box>
        </div>
        <AppButton onClick={goBack}>Back</AppButton>
      </div>

      {submission.screenshotUrl ? (
        <PanZoomImage
          src={imageUrl(submission.screenshotUrl)}
          alt="screenshot"
          height={600}
          containerStyle={{ maxWidth: 1000 }}
        />
      ) : (
        <Card sx={{ p: 2 }}>
          <Typography variant="body1">{missingScreenshotText}</Typography>
        </Card>
      )}

    </div>
  )
}
