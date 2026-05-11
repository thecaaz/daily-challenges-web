import React from 'react'
import { useParams, useNavigate, useLocation, Link } from 'react-router-dom'
import { Typography, Card, Box } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import PanZoomImage from '../components/ui/PanZoomImage'
import api from '../api'
import imageUrl from '../utils/imageUrl'
import goBackOrRoute from '../utils/navigation'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'
import useAsyncData from '../hooks/useAsyncData'

export default function SubmissionDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const { data: submission, loading } = useAsyncData(
    () => api.get(`/submissions/${id}`).then(r => r.data),
    [id]
  )

  if (loading) return <Loading />
  if (!submission) return <NotFound message="Not found" />

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <Typography variant="h5">
            Submission — {submission.userId ? (
              <Box component={Link} to={`/users/${submission.userId}`} sx={{ textDecoration: 'none', color: 'inherit' }}>
                {submission.username}
              </Box>
            ) : submission.username}
          </Typography>
          <Typography variant="caption">Score: {formatNumber(submission.score)} — {formatDateTime(submission.createdAt)}</Typography>
        </div>
        <AppButton onClick={() => goBackOrRoute(navigate, `/games/${submission.gameId}`, { locationSearch: location.search, scoringDay: submission?.scoringDay })}>Back</AppButton>
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
          <Typography variant="body1">No screenshot attached.</Typography>
        </Card>
      )}

    </div>
  )
}
