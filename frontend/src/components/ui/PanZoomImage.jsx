import React, { useEffect, useState, useRef } from 'react'
import { Box } from '@mui/material'
import AppButton from './AppButton'

export default function PanZoomImage({ src, alt, height = 500, containerStyle }) {
  const containerRef = useRef(null)
  const scaleRef = useRef(1)
  const offsetRef = useRef({ x: 0, y: 0 })
  const dragging = useRef(false)
  const lastPos = useRef({ x: 0, y: 0 })
  const [, forceRender] = useState(0)

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
        style={{
          width: '100%', height, border: '1px solid #ddd', borderRadius: 14,
          overflow: 'hidden', position: 'relative', cursor: 'grab', background: '#fafafa',
          ...containerStyle,
        }}
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
