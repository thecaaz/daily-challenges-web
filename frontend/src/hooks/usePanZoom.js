import { useCallback, useEffect, useRef, useState } from 'react'

const ZOOM_IN_FACTOR = 1.1
const ZOOM_OUT_FACTOR = 0.9
const MIN_SCALE = 0.1
const MAX_SCALE = 10
const SCALE_DECIMALS = 3
const INITIAL_VIEW = { scale: 1, offset: { x: 0, y: 0 } }

/**
 * Wheel-to-zoom (around the cursor) and drag-to-pan for the element in
 * `containerRef`. Dragging keeps tracking when the pointer leaves the element.
 *
 * @returns {{ containerRef: React.RefObject<HTMLElement>, scale: number,
 *   offset: { x: number, y: number }, reset: () => void }}
 */
export default function usePanZoom() {
  const containerRef = useRef(null)
  const viewRef = useRef(INITIAL_VIEW)
  const [, forceRender] = useState(0)

  const setView = useCallback((view) => {
    viewRef.current = view
    forceRender(renderCount => renderCount + 1)
  }, [])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return undefined
    return attachPanZoomListeners(container, () => viewRef.current, setView)
  }, [setView])

  const reset = useCallback(() => setView(INITIAL_VIEW), [setView])

  return { containerRef, scale: viewRef.current.scale, offset: viewRef.current.offset, reset }
}

function attachPanZoomListeners(container, getView, setView) {
  let lastDragPosition = null

  const handleWheel = (event) => {
    event.preventDefault()
    setView(zoomAroundCursor(getView(), event, container.getBoundingClientRect()))
  }

  const handleMouseDown = (event) => {
    lastDragPosition = { x: event.clientX, y: event.clientY }
    event.preventDefault()
  }

  const handleMouseMove = (event) => {
    if (!lastDragPosition) return
    const { offset } = getView()
    const deltaX = event.clientX - lastDragPosition.x
    const deltaY = event.clientY - lastDragPosition.y
    lastDragPosition = { x: event.clientX, y: event.clientY }
    setView({ ...getView(), offset: { x: offset.x + deltaX, y: offset.y + deltaY } })
  }

  const handleMouseUp = () => {
    lastDragPosition = null
  }

  container.addEventListener('wheel', handleWheel, { passive: false })
  container.addEventListener('mousedown', handleMouseDown)
  window.addEventListener('mousemove', handleMouseMove)
  window.addEventListener('mouseup', handleMouseUp)

  return () => {
    container.removeEventListener('wheel', handleWheel)
    container.removeEventListener('mousedown', handleMouseDown)
    window.removeEventListener('mousemove', handleMouseMove)
    window.removeEventListener('mouseup', handleMouseUp)
  }
}

/** Zooms one wheel step, keeping the image point under the cursor in place. */
function zoomAroundCursor({ scale, offset }, wheelEvent, containerRect) {
  const zoomFactor = wheelEvent.deltaY < 0 ? ZOOM_IN_FACTOR : ZOOM_OUT_FACTOR
  const nextScale = clampScale(+(scale * zoomFactor).toFixed(SCALE_DECIMALS))
  const scaleChange = nextScale / scale
  const cursorFromImageCentreX = wheelEvent.clientX - containerRect.left - containerRect.width / 2 - offset.x
  const cursorFromImageCentreY = wheelEvent.clientY - containerRect.top - containerRect.height / 2 - offset.y
  return {
    scale: nextScale,
    offset: {
      x: offset.x - cursorFromImageCentreX * (scaleChange - 1),
      y: offset.y - cursorFromImageCentreY * (scaleChange - 1),
    },
  }
}

function clampScale(scale) {
  return Math.max(MIN_SCALE, Math.min(MAX_SCALE, scale))
}
