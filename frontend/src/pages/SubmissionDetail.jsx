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

  const [scale, setScale] = useState(1)
  const [offset, setOffset] = useState({ x: 0, y: 0 })
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

  const onWheel = (e) => {
      if (!submission?.screenshotUrl) return
      const el = containerRef.current
      if (!el) return
      const rect = el.getBoundingClientRect()
      const mouseX = e.clientX - rect.left
      const mouseY = e.clientY - rect.top
      const centerX = rect.width / 2
      const centerY = rect.height / 2

      const delta = -e.deltaY
      const factor = delta > 0 ? 1.1 : 0.9

      setScale(prevScale => {
        const newScale = Math.max(0.1, Math.min(10, +(prevScale * factor).toFixed(3)))
        setOffset(prevOff => {
          const dx = mouseX - centerX - prevOff.x
          const dy = mouseY - centerY - prevOff.y
          const scaleChange = newScale / prevScale
          const newX = prevOff.x - dx * (scaleChange - 1)
          const newY = prevOff.y - dy * (scaleChange - 1)
          return { x: newX, y: newY }
        })
        return newScale
      })
  }

  // Attach a non-passive native wheel listener so we can reliably call
  // preventDefault() and stop the page from scrolling while zooming.
  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    const handler = (e) => {
      if (!submission?.screenshotUrl) return
      e.preventDefault()
      onWheel(e)
    }
    el.addEventListener('wheel', handler, { passive: false })
    return () => el.removeEventListener('wheel', handler, { passive: false })
  }, [submission])

  const onMouseDown = (e) => {
    if (!submission?.screenshotUrl) return
    dragging.current = true
    lastPos.current = { x: e.clientX, y: e.clientY }
    e.preventDefault()
  }
  const onMouseMove = (e) => {
    if (!dragging.current) return
    const dx = e.clientX - lastPos.current.x
    const dy = e.clientY - lastPos.current.y
    lastPos.current = { x: e.clientX, y: e.clientY }
    setOffset(prev => ({ x: prev.x + dx, y: prev.y + dy }))
  }
  const onMouseUp = () => { dragging.current = false }

  const resetView = () => { setScale(1); setOffset({ x: 0, y: 0 }) }

  if (loading) return <Loading />
  if (!submission) return <NotFound message="Not found" />

  const apiRoot = getApiRoot()

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
          onMouseDown={onMouseDown}
          onMouseMove={onMouseMove}
          onMouseUp={onMouseUp}
          onMouseLeave={onMouseUp}
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
