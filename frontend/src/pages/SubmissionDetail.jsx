import React, { useEffect, useState } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Typography, Card } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import PanZoomImage from '../components/ui/PanZoomImage'
import api from '../api'
import imageUrl from '../utils/imageUrl'
import goBackOrRoute from '../utils/navigation'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'

export default function SubmissionDetail() {
  const { id } = useParams()
  const [submission, setSubmission] = useState(null)
  const [loading, setLoading] = useState(true)
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    const fetchSubmission = async () => {
      try {
        const res = await api.get(`/submissions/${id}`)
        setSubmission(res.data)
      } catch (err) {
        // ignore
      } finally {
        setLoading(false)
      }
    }
    fetchSubmission()
  }, [])

  if (loading) return <Loading />
  if (!submission) return <NotFound message="Not found" />

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <Typography variant="h5">Submission — {submission.username}</Typography>
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
