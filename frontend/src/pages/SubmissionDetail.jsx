import React, { useEffect, useState, useRef } from 'react'
import { useParams, Link } from 'react-router-dom'
import { Typography, Button } from '@mui/material'
import api from '../api'

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
    const delta = -e.deltaY
    const factor = delta > 0 ? 1.1 : 0.9
    setScale(prev => Math.max(0.1, Math.min(10, +(prev * factor).toFixed(3))))
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

  if (loading) return <div>Loading...</div>
  if (!submission) return <div>Not found</div>

  const apiRoot = api.defaults.baseURL ? api.defaults.baseURL.replace(/\/api\/?$/, '') : 'http://localhost:5000'

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <Typography variant="h5">Submission — {submission.username ?? 'Anonymous'}</Typography>
          <Typography variant="caption">Score: {submission.score} — {new Date(submission.createdAt).toLocaleString()}</Typography>
        </div>
        <div>
          <Button component={Link} to={`/games/${submission.gameId}`} className="btn" sx={{ mr: 1 }}>Back</Button>
          <Button onClick={resetView} className="btn">Reset View</Button>
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
        <div className="card">
          <Typography variant="body1">No screenshot attached.</Typography>
        </div>
      )}

    </div>
  )
}
