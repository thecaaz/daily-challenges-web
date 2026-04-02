import React, { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Typography, Card, Box, Chip, Tooltip, Divider } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import AppButton from '../components/ui/AppButton'
import PanZoomImage from '../components/ui/PanZoomImage'
import api from '../api'
import { formatDateTime } from '../utils/dateFormat'
import formatNumber from '../utils/formatNumber'
import imageUrl from '../utils/imageUrl'
import goBackOrRoute from '../utils/navigation'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'

function SubmissionPanel({ submission }) {
  return (
    <Box sx={{ flex: 1, minWidth: 0 }}>
      <Box sx={{ mb: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {submission.username}
          </Typography>
          {submission.isDayWinner && (
            <Tooltip title={submission.scoringDay ? `Winner for ${submission.scoringDay}` : 'Winner'}>
              <EmojiEventsIcon sx={{ color: '#FFD700', fontSize: '1.2rem' }} />
            </Tooltip>
          )}
          {submission.rank && (
            <Chip label={`#${submission.rank}`} size="small" color="secondary" sx={{ fontWeight: 700, fontSize: '0.75rem' }} />
          )}
        </Box>
        <Typography variant="h5" sx={{ fontWeight: 800, lineHeight: 1.2 }}>{formatNumber(submission.score)}</Typography>
        <Typography variant="caption" color="text.secondary">
          {formatDateTime(submission.createdAt)}
          {submission.scoringDay && ` · Day: ${submission.scoringDay}`}
        </Typography>
      </Box>
      {submission.screenshotUrl ? (
        <PanZoomImage src={imageUrl(submission.screenshotUrl)} alt={`${submission.username ?? 'Anonymous'} screenshot`} />
      ) : (
        <Card sx={{ p: 3, textAlign: 'center', height: 500, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Typography color="text.secondary">No screenshot attached</Typography>
        </Card>
      )}
    </Box>
  )
}

export default function CompareSubmissions() {
  const { id1, id2 } = useParams()
  const navigate = useNavigate()
  const [sub1, setSub1] = useState(null)
  const [sub2, setSub2] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  useEffect(() => {
    let cancelled = false
    async function load() {
      try {
        const [r1, r2] = await Promise.all([
          api.get(`/submissions/${id1}`),
          api.get(`/submissions/${id2}`),
        ])
        if (!cancelled) { setSub1(r1.data); setSub2(r2.data) }
      } catch {
        if (!cancelled) setError(true)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => { cancelled = true }
  }, [id1, id2])

  if (loading) return <Loading />
  if (error || !sub1 || !sub2) return <NotFound message="Could not load submissions for comparison" />

  return (
    <div>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Compare Submissions</Typography>
        <AppButton onClick={() => goBackOrRoute(navigate, sub1.gameId ? `/games/${sub1.gameId}` : '/', {})}>Back</AppButton>
      </Box>

      <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
        <SubmissionPanel submission={sub1} />
        <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', md: 'block' } }} />
        <Divider sx={{ display: { xs: 'block', md: 'none' } }} />
        <SubmissionPanel submission={sub2} />
      </Box>
    </div>
  )
}
