import React from 'react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

vi.mock('../hooks/useAdapter', () => ({
  default: () => null,
  useHasAdapter: () => null,
}))
vi.mock('../hooks/useFavorite', () => ({
  default: (gameId, initial) => ({ isFavorite: Boolean(initial), toggle: vi.fn(), loading: false }),
}))

import GameHeader from '../components/GameHeader'

const NOW = new Date('2026-11-26T10:00:00Z')

const aGame = overrides => ({
  id: 4,
  name: 'Wordle',
  url: 'https://wordle.example/',
  imageUrl: null,
  isFavorite: false,
  currentScoringDayUtcEnd: '2026-11-26T12:00:00Z',
  ...overrides,
})

const renderHeader = game => render(<MemoryRouter><GameHeader game={game} /></MemoryRouter>)

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval', 'setTimeout', 'clearTimeout'] })
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('GameHeader', () => {
  it('renders nothing without a game', () => {
    const { container } = renderHeader(null)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows the fallback text without a description', () => {
    const { container } = renderHeader(aGame({ description: '   ' }))

    expect(screen.getByText('Compete on daily challenges — climb the leaderboard!')).toBeInTheDocument()
    expect(container.innerHTML).toMatchSnapshot()
  })

  it('shows a short description in full, without a tooltip', () => {
    const { container } = renderHeader(aGame({ description: '  Guess the word.  ' }))

    expect(screen.getByText('Guess the word.')).not.toHaveAttribute('aria-label')
    expect(container.innerHTML).toMatchSnapshot()
  })

  it('does not truncate a description of exactly 300 characters', () => {
    const description = 'a'.repeat(300)

    renderHeader(aGame({ description }))

    expect(screen.getByText(description)).toBeInTheDocument()
  })

  it('truncates a long description and shows it in full in a tooltip', () => {
    const description = 'b'.repeat(301)

    const { container } = renderHeader(aGame({ description }))

    const truncated = screen.getByText(`${'b'.repeat(300)}…`)
    expect(truncated).toHaveAttribute('aria-label', description)
    expect(truncated).toHaveStyle({ cursor: 'help' })
    expect(container.innerHTML).toMatchSnapshot()
  })
})
