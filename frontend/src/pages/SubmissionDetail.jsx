import React, { useEffect, useState, useRef } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Typography, Button, Card } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import api, { getApiRoot } from '../api'
import parseUtcDate from '../utils/parseUtcDate'
import formatNumber from '../utils/formatNumber'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'

export default function SubmissionDetail() {
  const { id } = useParams()
  const [submission, setSubmission] = useState(null)
  const [loading, setLoading] = useState(true)
  const imgRef = useRef(null)
  const containerRef = useRef(null)

  const scaleRef = useRef(1)
  const offsetRef = useRef({ x: 0, y: 0 })
  const [, forceRender] = useState(0)
  const dragging = useRef(false)
  const lastPos = useRef({ x: 0, y: 0 })

  useEffect(() => { fetchSubmission() }, [])
  const navigate = useNavigate()
  const location = useLocation()

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

  // All pan/zoom via native listeners to avoid stale closure issues
  // Must depend on submission so listeners attach after conditional render
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
  }, [submission])

  const resetView = () => {
    scaleRef.current = 1
    offsetRef.current = { x: 0, y: 0 }
    forceRender(n => n + 1)
  }

  if (loading) return <Loading />
  if (!submission) return <NotFound message="Not found" />

  const apiRoot = getApiRoot()
  const scale = scaleRef.current
  const offset = offsetRef.current

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <Typography variant="h5">Submission — {submission.username ?? 'Anonymous'}</Typography>
          <Typography variant="caption">Score: {formatNumber(submission.score)} — {parseUtcDate(submission.createdAt).toLocaleString()}</Typography>
        </div>
        <div>
          <AppButton onClick={() => {
            // Prefer history back so the previous page (with query params/state) is preserved.
            if (window.history.length > 1) navigate(-1)
            else {
              const qs = location.search || (submission?.scoringDay ? `?scoringDay=${submission.scoringDay}` : '')
              navigate(`/games/${submission.gameId}${qs}`)
            }
          }} sx={{ mr: 1 }}>Back</AppButton>
          <AppButton onClick={resetView}>Reset View</AppButton>
        </div>
      </div>

      {submission.screenshotUrl ? (
        <div
          ref={containerRef}
          style={{ width: '100%', maxWidth: 1000, height: 600, border: '1px solid #ddd', overflow: 'hidden', position: 'relative', cursor: 'grab' }}>
          <img
            ref={imgRef}
            src={`${apiRoot}${submission.screenshotUrl}`}
            alt="screenshot"
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
              translate: '-50% -50%'
            }}
          />
        </div>
      ) : (
        <Card sx={{ p: 2 }}>
          <Typography variant="body1">No screenshot attached.</Typography>
        </Card>
      )}

    </div>
  )
}
