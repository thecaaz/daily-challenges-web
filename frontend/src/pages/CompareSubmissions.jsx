import React, { useEffect, useState, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Typography, Card, Box, Chip, Tooltip, Divider } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import AppButton from '../components/ui/AppButton'
import api, { getApiRoot } from '../api'
import parseUtcDate from '../utils/parseUtcDate'
import formatNumber from '../utils/formatNumber'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'

function PanZoomImage({ src, alt }) {
  const containerRef = useRef(null)
  const scaleRef = useRef(1)
  const offsetRef = useRef({ x: 0, y: 0 })
  const dragging = useRef(false)
  const lastPos = useRef({ x: 0, y: 0 })
  const [, forceRender] = useState(0)

  const getScale = () => scaleRef.current
  const getOffset = () => offsetRef.current

  const updateScale = (val) => { scaleRef.current = val; forceRender(n => n + 1) }
  const updateOffset = (val) => { offsetRef.current = val; forceRender(n => n + 1) }

  useEffect(() => {
    const el = containerRef.current
    if (!el) return

    const handleWheel = (e) => {
      e.preventDefault()
      const rect = el.getBoundingClientRect()
      const mouseX = e.clientX - rect.left
      const mouseY = e.clientY - rect.top
      const centerX = rect.width / 2
      const centerY = rect.height / 2
      const delta = -e.deltaY
      const factor = delta > 0 ? 1.1 : 0.9

      const prev = scaleRef.current
      const next = Math.max(0.1, Math.min(10, +(prev * factor).toFixed(3)))
      const off = offsetRef.current
      const dx = mouseX - centerX - off.x
      const dy = mouseY - centerY - off.y
      const sc = next / prev
      offsetRef.current = { x: off.x - dx * (sc - 1), y: off.y - dy * (sc - 1) }
      scaleRef.current = next
      forceRender(n => n + 1)
    }

    const handleMouseDown = (e) => {
      dragging.current = true
      lastPos.current = { x: e.clientX, y: e.clientY }
      e.preventDefault()
    }

    const handleMouseMove = (e) => {
      if (!dragging.current) return
      const dx = e.clientX - lastPos.current.x
      const dy = e.clientY - lastPos.current.y
      lastPos.current = { x: e.clientX, y: e.clientY }
      const off = offsetRef.current
      offsetRef.current = { x: off.x + dx, y: off.y + dy }
      forceRender(n => n + 1)
    }

    const handleMouseUp = () => {
      dragging.current = false
    }

    el.addEventListener('wheel', handleWheel, { passive: false })
    el.addEventListener('mousedown', handleMouseDown)
    window.addEventListener('mousemove', handleMouseMove)
    window.addEventListener('mouseup', handleMouseUp)

    return () => {
      el.removeEventListener('wheel', handleWheel)
      el.removeEventListener('mousedown', handleMouseDown)
      window.removeEventListener('mousemove', handleMouseMove)
      window.removeEventListener('mouseup', handleMouseUp)
    }
  }, [])

  const reset = () => {
    scaleRef.current = 1
    offsetRef.current = { x: 0, y: 0 }
    forceRender(n => n + 1)
  }

  const scale = scaleRef.current
  const offset = offsetRef.current
  return (
    <div>
      <div
        ref={containerRef}
        style={{ width: '100%', height: 500, border: '1px solid #ddd', borderRadius: 14, overflow: 'hidden', position: 'relative', cursor: 'grab', background: '#fafafa' }}
      >
        <img
          src={src}
          alt={alt}
          style={{
            transform: `translate(${offset.x}px, ${offset.y}px) scale(${scale})`,
            transformOrigin: 'center center',
            userSelect: 'none',
            pointerEvents: 'none',
            position: 'absolute',
            left: '50%',
            top: '50%',
            maxWidth: 'none',
            width: 'auto',
            height: 'auto',
            minWidth: 0,
            minHeight: 0,
            translate: '-50% -50%',
          }}
        />
      </div>
      <Box sx={{ mt: 1, textAlign: 'center' }}>
        <AppButton size="small" onClick={reset}>Reset View</AppButton>
      </Box>
    </div>
  )
}

function SubmissionPanel({ submission }) {
  const apiRoot = getApiRoot()
  return (
    <Box sx={{ flex: 1, minWidth: 0 }}>
      <Box sx={{ mb: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {submission.username ?? 'Anonymous'}
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
          {parseUtcDate(submission.createdAt).toLocaleString()}
          {submission.scoringDay && ` · Day: ${submission.scoringDay}`}
        </Typography>
      </Box>
      {submission.screenshotUrl ? (
        <PanZoomImage src={`${apiRoot}${submission.screenshotUrl}`} alt={`${submission.username ?? 'Anonymous'} screenshot`} />
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
        <AppButton onClick={() => {
          if (window.history.length > 1) navigate(-1)
          else navigate(sub1.gameId ? `/games/${sub1.gameId}` : '/')
        }}>Back</AppButton>
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
