import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

vi.mock('../hooks/useRequireAuth', () => ({
  default: () => ({ user: { id: 1, username: 'me' }, loading: false, fetchMe: vi.fn() }),
}))

const GAMES = [
  { id: 1, name: 'Game A', url: 'https://a.example/', isFavorite: true },
  { id: 2, name: 'Game B', url: 'https://b.example/', isFavorite: true },
]
vi.mock('../hooks/usePlayableFavorites', () => ({
  default: () => ({
    total: 2, submittedCount: 0, pendingGames: GAMES, playable: GAMES, playableCount: 2,
    loading: false, checkingPlayable: false, error: false, reload: () => {},
  }),
}))

// Stand-in for the extension capture hook: real React state, plus a handle
// so the test can simulate what a capture does.
const capture = { current: null }
const cancelCapture = vi.fn()
vi.mock('../hooks/useScoreCapture', async () => {
  const { useState } = await vi.importActual('react')
  return {
    useScoreCapture: () => {
      const [score, setScore] = useState('')
      const [capturedFile, setCapturedFile] = useState(null)
      const [showSubmission, setShowSubmission] = useState(false)
      const [capturing, setCapturing] = useState(false)
      const api = {
        score, setScore, capturedFile, setCapturedFile, showSubmission, setShowSubmission,
        submitScore: () => {}, canCapture: true, capturing, setCapturing, captureError: null,
        cancelCapture: () => { cancelCapture(); setCapturing(false) },
      }
      capture.current = api
      return api
    },
  }
})

vi.mock('../components/ui/GameIframePlayer', () => ({
  default: ({ title }) => <div data-testid="iframe">{title}</div>,
}))

vi.mock('../components/SubmissionForm/SubmissionForm', () => ({
  default: ({ gameId, initialScore, initialScreenshot }) => (
    <div data-testid="form">
      <span data-testid="form-game">{String(gameId)}</span>
      <span data-testid="form-score">{initialScore}</span>
      <span data-testid="form-file">{initialScreenshot ? initialScreenshot.name : ''}</span>
    </div>
  ),
}))

import DashboardPlay from '../pages/DashboardPlay'

function renderPage() {
  return render(<MemoryRouter><DashboardPlay /></MemoryRouter>)
}

describe('DashboardPlay', () => {
  beforeEach(() => {
    capture.current = null
    cancelCapture.mockClear()
  })

  it('shows Submit only when the extension can capture', () => {
    renderPage()
    expect(screen.getByRole('button', { name: 'Submit' })).toBeInTheDocument()
  })

  it('capture → Skip → the next game starts with an empty form', () => {
    renderPage()
    expect(screen.getByRole('heading', { name: 'Game A' })).toBeInTheDocument()

    // Simulate a successful capture on game A.
    act(() => {
      capture.current.setScore('12345')
      capture.current.setCapturedFile(new File(['x'], 'a.webp', { type: 'image/webp' }))
      capture.current.setShowSubmission(true)
    })
    expect(screen.getByTestId('form-score')).toHaveTextContent('12345')
    expect(screen.getByTestId('form-file')).toHaveTextContent('a.webp')

    fireEvent.click(screen.getByRole('button', { name: 'Skip' }))

    // Game B: back to the iframe, captured data dropped.
    expect(screen.getByRole('heading', { name: 'Game B' })).toBeInTheDocument()
    expect(screen.getByTestId('iframe')).toHaveTextContent('Game B')
    expect(screen.queryByTestId('form')).not.toBeInTheDocument()
    expect(capture.current.score).toBe('')
    expect(capture.current.capturedFile).toBeNull()

    // Opening the form for game B (e.g. a capture that found nothing) shows no leftovers.
    act(() => { capture.current.setShowSubmission(true) })
    expect(screen.getByTestId('form-game')).toHaveTextContent('2')
    expect(screen.getByTestId('form-score')).toHaveTextContent('')
    expect(screen.getByTestId('form-file')).toHaveTextContent('')
  })

  it('disables Skip while a capture is running', () => {
    renderPage()
    act(() => { capture.current.setCapturing(true) })
    expect(screen.getByRole('button', { name: 'Skip' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Capturing…' })).toBeDisabled()

    act(() => { capture.current.setCapturing(false) })
    expect(screen.getByRole('button', { name: 'Skip' })).toBeEnabled()
  })

  it('Skip cancels any capture in flight before moving to the next game', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Skip' }))
    expect(cancelCapture).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('heading', { name: 'Game B' })).toBeInTheDocument()
  })

  it('shows "All done" after the last game', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Skip' }))
    fireEvent.click(screen.getByRole('button', { name: 'Skip' }))
    expect(screen.getByText('All done')).toBeInTheDocument()
  })
})
