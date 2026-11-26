import React from 'react'
import { describe, it, expect } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'

import PanZoomImage from '../components/ui/PanZoomImage'

const CONTAINER_RECT = { left: 0, top: 0, width: 200, height: 100, right: 200, bottom: 100, x: 0, y: 0 }

function renderImage() {
  const { container } = render(<PanZoomImage src="/shot.png" alt="Screenshot" height={300} />)
  const viewport = screen.getByRole('img', { name: 'Screenshot' }).parentElement
  viewport.getBoundingClientRect = () => CONTAINER_RECT
  return { container, viewport, image: () => screen.getByRole('img', { name: 'Screenshot' }) }
}

const CENTRE = { clientX: 100, clientY: 50 }

function wheel(viewport, deltaY, position) {
  const event = new Event('wheel', { bubbles: true, cancelable: true })
  Object.defineProperties(event, {
    deltaY: { value: deltaY },
    clientX: { value: position.clientX },
    clientY: { value: position.clientY },
  })
  act(() => { viewport.dispatchEvent(event) })
}

const zoomIn = (viewport, position = CENTRE) => wheel(viewport, -100, position)
const zoomOut = (viewport, position = CENTRE) => wheel(viewport, 100, position)

describe('PanZoomImage', () => {
  it('renders the same markup as before the refactor', () => {
    const { container } = renderImage()

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('zooms in by 10% per wheel step around the centre', () => {
    const { viewport, image } = renderImage()

    zoomIn(viewport)
    zoomIn(viewport)

    expect(image().style.transform).toBe('translate(0px, 0px) scale(1.21)')
  })

  it('zooms out by 10% per wheel step', () => {
    const { viewport, image } = renderImage()

    zoomOut(viewport)

    expect(image().style.transform).toBe('translate(0px, 0px) scale(0.9)')
  })

  it('keeps the point under the cursor in place while zooming', () => {
    const { viewport, image } = renderImage()

    zoomIn(viewport, { clientX: 150, clientY: 20 })

    expect(image().style.transform).toMatchSnapshot()
  })

  it('clamps the zoom between 0.1 and 10', () => {
    const { viewport, image } = renderImage()

    for (let step = 0; step < 60; step++) zoomIn(viewport)
    expect(image().style.transform).toBe('translate(0px, 0px) scale(10)')

    for (let step = 0; step < 120; step++) zoomOut(viewport)
    expect(image().style.transform).toBe('translate(0px, 0px) scale(0.1)')
  })

  it('pans while dragging and stops on mouse up', () => {
    const { viewport, image } = renderImage()

    fireEvent.mouseDown(viewport, { clientX: 10, clientY: 10 })
    fireEvent.mouseMove(window, { clientX: 40, clientY: -5 })
    fireEvent.mouseUp(window)
    fireEvent.mouseMove(window, { clientX: 90, clientY: 90 })

    expect(image().style.transform).toBe('translate(30px, -15px) scale(1)')
  })

  it('resets the view', () => {
    const { viewport, image } = renderImage()
    zoomIn(viewport, { clientX: 150, clientY: 20 })

    fireEvent.click(screen.getByRole('button', { name: 'Reset View' }))

    expect(image().style.transform).toBe('translate(0px, 0px) scale(1)')
  })
})
