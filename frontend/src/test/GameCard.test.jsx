import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

let mockAdapter = null
vi.mock('../hooks/useAdapter', () => ({
  default: () => mockAdapter,
  useHasAdapter: () => null,
}))
vi.mock('../hooks/useFavorite', () => ({
  default: (gameId, initial) => ({ isFavorite: Boolean(initial), toggle: vi.fn(), loading: false }),
}))

import GameCard from '../components/ui/GameCard/GameCard'

const aGame = overrides => ({
  id: 4,
  name: 'Wordle',
  url: 'https://wordle.example/',
  imageUrl: null,
  isFavorite: false,
  hasSubmittedForLatest: false,
  ...overrides,
})

const renderCard = game => render(<MemoryRouter><GameCard game={game} /></MemoryRouter>)

beforeEach(() => {
  mockAdapter = null
})

describe('GameCard', () => {
  it('renders the same markup as before the refactor', () => {
    mockAdapter = { name: 'Wordle adapter' }

    const { container } = renderCard(aGame())

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('renders the same submitted card as before the refactor', () => {
    const { container } = renderCard(aGame({ hasSubmittedForLatest: true, url: null }))

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('links to the score submission while not yet submitted', () => {
    renderCard(aGame())

    expect(screen.getByRole('link', { name: 'Submit score' })).toHaveAttribute('href', '/submit/4')
  })

  it('disables submitting once submitted', () => {
    renderCard(aGame({ hasSubmittedForLatest: true }))

    expect(screen.getByRole('button', { name: 'Already submitted today' })).toBeDisabled()
  })

  it('offers no Play button without a game URL', () => {
    renderCard(aGame({ url: null }))

    expect(screen.queryByRole('link', { name: 'Play' })).toBeNull()
  })
})
