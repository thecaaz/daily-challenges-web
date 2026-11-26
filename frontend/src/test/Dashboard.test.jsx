import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const mockAuthValue = { user: { id: 1, username: 'me' }, loading: false, fetchMe: vi.fn() }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuthValue,
}))

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...a) => mockGet(...a) },
  getApiRoot: () => '',
}))

let mockFavorites
vi.mock('../hooks/usePlayableFavorites', () => ({
  default: () => mockFavorites,
}))

vi.mock('../components/dashboard/GameRow', () => ({
  default: ({ game, badgeLabel }) => (
    <li data-testid="game-row">{game.name}|fav:{String(!!game.isFavorite)}|{badgeLabel ?? ''}</li>
  ),
}))
vi.mock('../components/dashboard/FriendActivityRow', () => ({
  default: ({ activity }) => <li data-testid="activity-row">{activity.gameName}|fav:{String(!!activity.isFavorite)}</li>,
}))
vi.mock('../components/dashboard/RankRow', () => ({
  default: ({ rank }) => <li data-testid="rank-row">{rank.gameName}#{rank.rank}</li>,
}))
vi.mock('../components/dashboard/FriendsPanel', () => ({
  default: ({ friends }) => <div data-testid="friends-panel">{friends.length} friends</div>,
}))

import Dashboard from '../pages/Dashboard'

const NO_FAVORITES = {
  total: 0, submittedCount: 0, pendingGames: [], playable: [], playableCount: 0,
  loading: false, checkingPlayable: false, error: false, reload: () => {},
}

const dashboard = {
  recentGames: [
    { game: { id: 3, name: 'Wordle', isFavorite: false }, todayCount: 2 },
    { game: { id: 4, name: 'Cutle', isFavorite: true }, todayCount: 1 },
  ],
  friendActivity: [{ gameId: 3, gameName: 'Wordle', isFavorite: false }],
  friends: [{ userId: 9, username: 'pal' }],
  xpEarnedToday: 25,
  userTodayRanks: [{ gameId: 4, gameName: 'Cutle', rank: 1 }],
  activeGameCount: 12,
  submissionCountToday: 40,
}

function renderPage() {
  return render(<MemoryRouter><Dashboard /></MemoryRouter>)
}

function statValue(label) {
  return screen.getByText(label).previousSibling.textContent
}

describe('Dashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockFavorites = NO_FAVORITES
  })

  it('shows the server totals, friend count, activity and XP stat cards', async () => {
    mockGet.mockResolvedValue({ data: dashboard })
    renderPage()

    await screen.findByText('Active today')
    expect(statValue('Active today')).toBe('12')
    expect(statValue('Submissions today')).toBe('40')
    expect(statValue('Friends')).toBe('1')
    expect(statValue('Friend activity')).toBe('1 game')
    expect(statValue('XP earned today')).toBe('+25')
    expect(mockGet).toHaveBeenCalledWith('/dashboard')
  })

  it('falls back to counting the lists when the server sends no totals', async () => {
    mockGet.mockResolvedValue({ data: { ...dashboard, activeGameCount: undefined, submissionCountToday: undefined } })
    renderPage()

    await screen.findByText('Active today')
    expect(statValue('Active today')).toBe('2')
    expect(statValue('Submissions today')).toBe('3')
  })

  it('hides friend activity and XP cards when there are none', async () => {
    mockGet.mockResolvedValue({ data: { ...dashboard, friendActivity: [], xpEarnedToday: 0 } })
    renderPage()

    await screen.findByText('Active today')
    expect(screen.queryByText('Friend activity')).not.toBeInTheDocument()
    expect(screen.queryByText('XP earned today')).not.toBeInTheDocument()
  })

  it('lists recent games with a player badge, friend activity and ranks', async () => {
    mockGet.mockResolvedValue({ data: dashboard })
    renderPage()

    await screen.findByText('Active Today')
    const games = screen.getAllByTestId('game-row').map(r => r.textContent)
    expect(games).toEqual(['Wordle|fav:false|2 players today', 'Cutle|fav:true|1 player today'])
    expect(screen.getByTestId('activity-row')).toHaveTextContent('Wordle|fav:false')
    expect(screen.getByText('Your Ranks Today')).toBeInTheDocument()
    expect(screen.getByTestId('rank-row')).toHaveTextContent('Cutle#1')
    expect(screen.getByTestId('friends-panel')).toHaveTextContent('1 friends')
  })

  it('shows the empty texts when there is no activity', async () => {
    mockGet.mockResolvedValue({ data: {} })
    renderPage()

    expect(await screen.findByText('No games have submissions today yet.')).toBeInTheDocument()
    expect(screen.getByText("Add friends to see what they're playing.")).toBeInTheDocument()
    expect(screen.queryByText('Your Ranks Today')).not.toBeInTheDocument()
    expect(screen.queryByText('Pending Favorites')).not.toBeInTheDocument()
  })

  it('tells a user with friends that nobody played recently', async () => {
    mockGet.mockResolvedValue({ data: { friends: [{ userId: 9, username: 'pal' }] } })
    renderPage()

    expect(await screen.findByText('No friend activity in the last 7 days.')).toBeInTheDocument()
  })

  it('shows favorites progress, pending favorites and the play count', async () => {
    mockFavorites = {
      ...NO_FAVORITES,
      total: 3, submittedCount: 1, playableCount: 2,
      pendingGames: [{ id: 5, name: 'MapTap' }, { id: 6, name: 'BlindCut' }],
    }
    mockGet.mockResolvedValue({ data: {} })
    renderPage()

    expect(await screen.findByText("Today's Favorites")).toBeInTheDocument()
    expect(screen.getByText('1 / 3 done')).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '33')
    expect(screen.getByText('Pending Favorites')).toBeInTheDocument()
    expect(screen.getByText(/MapTap/)).toBeInTheDocument()
    const play = screen.getByRole('link', { name: 'Play (2)' })
    expect(play).toHaveAttribute('href', '/dashboard/play')
  })

  it('shows an ellipsis while checking playable games and disables Play when none', async () => {
    mockFavorites = { ...NO_FAVORITES, checkingPlayable: true }
    mockGet.mockResolvedValue({ data: {} })
    renderPage()

    const play = await screen.findByText('Play (…)')
    expect(play.closest('a')).toHaveAttribute('aria-disabled', 'true')
  })

  it('shows an error with Retry when the dashboard fails to load', async () => {
    mockGet.mockRejectedValueOnce(new Error('boom'))
    renderPage()

    expect(await screen.findByText('Could not load the dashboard.')).toBeInTheDocument()
    mockGet.mockResolvedValueOnce({ data: dashboard })
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('Active today')).toBeInTheDocument()
    expect(mockGet).toHaveBeenCalledTimes(2)
  })

  it('updates favorite flags when a favorite-changed event fires', async () => {
    mockGet.mockResolvedValue({ data: dashboard })
    renderPage()
    await screen.findByText('Active today')

    act(() => {
      window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { gameId: '3', isFavorite: true } }))
    })

    await waitFor(() => expect(screen.getAllByTestId('game-row')[0]).toHaveTextContent('Wordle|fav:true'))
    expect(screen.getByTestId('activity-row')).toHaveTextContent('Wordle|fav:true')
    expect(screen.getAllByTestId('game-row')[1]).toHaveTextContent('Cutle|fav:true')
  })

  it('ignores favorite-changed events without a game id', async () => {
    mockGet.mockResolvedValue({ data: dashboard })
    renderPage()
    await screen.findByText('Active today')

    act(() => {
      window.dispatchEvent(new CustomEvent('favorite-changed', { detail: { isFavorite: true } }))
    })

    expect(screen.getAllByTestId('game-row')[0]).toHaveTextContent('Wordle|fav:false')
  })
})
