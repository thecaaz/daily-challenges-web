import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

let mockAdapter = null
vi.mock('../hooks/useAdapter', () => ({
  default: () => mockAdapter,
  useHasAdapter: () => null,
}))

const mockToggle = vi.fn()
const mockUseFavorite = vi.fn()
vi.mock('../hooks/useFavorite', () => ({
  default: (...args) => mockUseFavorite(...args),
}))

import GameRow from '../components/dashboard/GameRow'
import FriendActivityRow from '../components/dashboard/FriendActivityRow'
import FavoriteButton from '../components/ui/FavoriteButton'

const FIVE_MINUTES_MS = 5 * 60 * 1000

const inRouter = ui => render(<MemoryRouter>{ui}</MemoryRouter>)

const aGame = overrides => ({
  id: 3,
  name: 'Wordle',
  url: 'https://wordle.example/',
  imageUrl: null,
  isFavorite: false,
  hasSubmittedForLatest: false,
  ...overrides,
})

const aSubmission = (username, overrides) => ({
  userId: username.length,
  username,
  submittedAt: new Date(Date.now() - FIVE_MINUTES_MS).toISOString(),
  ...overrides,
})

const anActivity = overrides => ({
  gameId: 9,
  gameName: 'Connections',
  gameUrl: 'https://connections.example/',
  gameImageUrl: null,
  isFavorite: true,
  hasSubmittedForLatest: true,
  recentSubmissions: [aSubmission('alice')],
  ...overrides,
})

beforeEach(() => {
  mockAdapter = null
  mockToggle.mockReset()
  mockUseFavorite.mockReset()
  mockUseFavorite.mockImplementation((gameId, initial) => ({ isFavorite: Boolean(initial), toggle: mockToggle, loading: false }))
})

describe('GameRow', () => {
  it('renders the same markup as before the refactor', () => {
    mockAdapter = { id: 'wordle' }

    const { container } = inRouter(<GameRow game={aGame({ hasSubmittedForLatest: true })} badge={2} badgeLabel="2 players today" />)

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('links the game name to the game page', () => {
    inRouter(<GameRow game={aGame()} />)

    expect(screen.getByRole('link', { name: 'Wordle' })).toHaveAttribute('href', '/games/3')
  })

  it('shows the badge label when a badge is given', () => {
    inRouter(<GameRow game={aGame()} badge={0} badgeLabel="0 players today" />)

    expect(screen.getByText('0 players today')).toBeInTheDocument()
  })

  it('shows no extension or submitted icon when neither applies', () => {
    const { container } = inRouter(<GameRow game={aGame()} />)

    expect(container.querySelector('[data-testid="ExtensionIcon"]')).toBeNull()
    expect(container.querySelector('[data-testid="CheckCircleIcon"]')).toBeNull()
  })
})

describe('FriendActivityRow', () => {
  it('renders the same markup as before the refactor', () => {
    mockAdapter = { id: 'connections' }

    const { container } = inRouter(<FriendActivityRow activity={anActivity()} />)

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('shows each submitter once, ignoring case', () => {
    const recentSubmissions = [aSubmission('alice'), aSubmission('ALICE', { userId: 99 }), aSubmission('bob')]

    inRouter(<FriendActivityRow activity={anActivity({ recentSubmissions })} />)

    expect(screen.getAllByText(/^(alice|ALICE|bob)$/).map(chip => chip.textContent)).toEqual(['alice', 'bob'])
  })

  it('shows at most four submitters', () => {
    const recentSubmissions = ['a', 'bb', 'ccc', 'dddd', 'eeeee'].map(name => aSubmission(name))

    inRouter(<FriendActivityRow activity={anActivity({ recentSubmissions })} />)

    expect(screen.queryByText('eeeee')).toBeNull()
    expect(screen.getByText('dddd')).toBeInTheDocument()
  })

  it('shows how long ago the latest submission was', () => {
    inRouter(<FriendActivityRow activity={anActivity()} />)

    expect(screen.getByText('5m ago')).toBeInTheDocument()
  })

  it('favourites the activity game', () => {
    inRouter(<FriendActivityRow activity={anActivity()} />)

    expect(mockUseFavorite).toHaveBeenCalledWith(9, true)
    expect(screen.getByRole('button', { name: 'Remove Connections from favorites' })).toBeInTheDocument()
  })

  it('opens the game URL without an adapter', () => {
    inRouter(<FriendActivityRow activity={anActivity()} />)

    expect(screen.getByRole('link', { name: 'Play' })).toHaveAttribute('href', 'https://connections.example/')
  })
})

describe('FavoriteButton', () => {
  it('names the game in its label', () => {
    inRouter(<FavoriteButton game={aGame()} />)

    expect(screen.getByRole('button', { name: 'Add Wordle to favorites' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('falls back to a generic label without a game name', () => {
    inRouter(<FavoriteButton game={{ id: 1, isFavorite: true }} />)

    expect(screen.getByRole('button', { name: 'Remove from favorites' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('toggles the favourite when clicked', () => {
    inRouter(<FavoriteButton game={aGame()} />)

    fireEvent.click(screen.getByRole('button', { name: 'Add Wordle to favorites' }))

    expect(mockToggle).toHaveBeenCalledTimes(1)
  })

  it('is disabled while saving', () => {
    mockUseFavorite.mockReturnValue({ isFavorite: false, toggle: mockToggle, loading: true })

    inRouter(<FavoriteButton game={aGame()} />)

    expect(screen.getByRole('button', { name: 'Add Wordle to favorites' })).toBeDisabled()
  })

  it('renders the same markup as before the refactor', () => {
    const { container } = inRouter(<FavoriteButton game={aGame()} sx={{ color: 'white' }} />)

    expect(container.innerHTML).toMatchSnapshot()
  })
})
