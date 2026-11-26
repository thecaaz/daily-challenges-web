import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

let mockUser = { id: 1, username: 'me' }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: mockUser }),
}))
const mockShowSnackbar = vi.fn()
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => ({ showSnackbar: mockShowSnackbar }),
}))

const mockGet = vi.fn()
const mockPost = vi.fn()
const mockDelete = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...args) => mockGet(...args),
    post: (...args) => mockPost(...args),
    delete: (...args) => mockDelete(...args),
  },
  getApiRoot: () => '',
}))

import UserProfileHeader from '../components/Profile/UserProfileHeader'
import TopGamesList from '../components/Profile/TopGamesList'
import AchievementsGrid from '../components/Profile/AchievementsGrid'
import FriendButton from '../components/Profile/FriendButton'

const inRouter = ui => render(<MemoryRouter>{ui}</MemoryRouter>)

function routeGets(routes) {
  mockGet.mockImplementation(url => {
    if (url in routes) {
      const response = routes[url]
      return response instanceof Error ? Promise.reject(response) : Promise.resolve({ data: response })
    }
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

describe('UserProfileHeader', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUser = null
  })

  const profile = {
    userId: 7,
    username: 'carol',
    level: 4,
    xpIntoLevel: 30,
    xpToNextLevel: 70,
    totalXp: 1234,
    streak: 5,
    lastSubmissionAt: new Date().toISOString(),
  }

  it('shows the level, XP, streak, last submission and level progress', () => {
    inRouter(<UserProfileHeader profile={profile} />)

    expect(screen.getByRole('link', { name: 'carol' })).toHaveAttribute('href', '/users/7')
    expect(screen.getByText('Level 4')).toBeInTheDocument()
    expect(screen.getByText(`${(1234).toLocaleString()} XP`)).toBeInTheDocument()
    expect(screen.getByText('Streak: 5')).toBeInTheDocument()
    expect(screen.getByText('Last: just now')).toBeInTheDocument()
    expect(screen.getByText('30/100 XP (70 to go) — 30%')).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '30')
  })

  it('falls back to defaults for missing fields', () => {
    inRouter(<UserProfileHeader profile={{}} />)

    expect(screen.getByText('user')).toBeInTheDocument()
    expect(screen.queryByRole('link')).toBeNull()
    expect(screen.getByText('Level 1')).toBeInTheDocument()
    expect(screen.getByText('0 XP')).toBeInTheDocument()
    expect(screen.getByText('Streak: 0')).toBeInTheDocument()
    expect(screen.queryByText(/^Last:/)).toBeNull()
    expect(screen.getByText('0/0 XP (0 to go) — 0%')).toBeInTheDocument()
  })
})

describe('TopGamesList', () => {
  const game = {
    gameId: 3,
    name: 'Wordle',
    plays: 12,
    highestScore: 4,
    bestSubmissionId: 99,
    lastPlayed: new Date().toISOString(),
    rankingMode: 'lowest',
    scoreHistory: [{ scoringDay: '2026-01-01', scoreValue: 4 }],
  }

  it('says so when there are no top games', () => {
    inRouter(<TopGamesList games={[]} />)

    expect(screen.getByText('No top games yet.')).toBeInTheDocument()
  })

  it('shows each game with plays, best score link, last play and the score trend', () => {
    inRouter(<TopGamesList games={[game]} />)

    expect(screen.getByRole('link', { name: 'Wordle' })).toHaveAttribute('href', '/games/3')
    expect(screen.getByText('Plays: 12')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '4' })).toHaveAttribute('href', '/submission/99')
    expect(screen.getByText('Last played: just now')).toBeInTheDocument()
    expect(screen.getByText('lower is better')).toBeInTheDocument()
  })

  it('shows the best score without a link, or a dash without one', () => {
    inRouter(<TopGamesList games={[
      { gameId: 4, name: 'A', highestScore: 7 },
      { gameId: 5 },
    ]} />)

    expect(screen.getByText('Best: 7')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: '7' })).toBeNull()
    expect(screen.getByText('Game')).toBeInTheDocument()
    expect(screen.getByText('Best: —')).toBeInTheDocument()
    expect(screen.getAllByText('Plays: 0')).toHaveLength(2)
  })

  it('hides the score trend when no day has a score', () => {
    inRouter(<TopGamesList games={[{ ...game, scoreHistory: [{ scoringDay: '2026-01-01', scoreValue: null }] }]} />)

    expect(screen.queryByText('lower is better')).toBeNull()
  })
})

describe('AchievementsGrid', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const achievements = [
    { achievementId: 'win_1', name: 'First Win', description: 'Win a day', iconKey: 'win_1', unlockedAt: '2026-01-01T00:00:00Z' },
    { achievementId: 'streak_7', name: 'Week Streak', description: 'Seven days', iconKey: 'streak_7' },
  ]

  it('lists unlocked achievements before locked ones with the unlocked count', async () => {
    routeGets({ '/users/7/achievements': [achievements[1], achievements[0]] })

    inRouter(<AchievementsGrid userId={7} />)

    expect(await screen.findByText('1/2')).toBeInTheDocument()
    const names = screen.getAllByText(/First Win|Week Streak/).map(element => element.textContent)
    expect(names).toEqual(['First Win', 'Week Streak'])
  })

  it('shows a spinner while loading', () => {
    mockGet.mockReturnValue(new Promise(() => {}))

    inRouter(<AchievementsGrid userId={7} />)

    expect(screen.getByRole('progressbar')).toBeInTheDocument()
  })

  it('renders nothing when the achievements cannot be loaded', async () => {
    routeGets({ '/users/7/achievements': new Error('down') })

    const { container } = inRouter(<AchievementsGrid userId={7} />)

    await waitFor(() => expect(container).toBeEmptyDOMElement())
  })

  it('renders nothing and loads nothing without a user', () => {
    const { container } = inRouter(<AchievementsGrid userId={null} />)

    expect(container).toBeEmptyDOMElement()
    expect(mockGet).not.toHaveBeenCalled()
  })
})

describe('FriendButton', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUser = { id: 1, username: 'me' }
  })

  const renderButton = () => inRouter(<FriendButton targetUserId={7} />)

  it('renders nothing on your own profile', () => {
    routeGets({ '/friends/status/7': { status: 'none' } })
    mockUser = { id: 7 }
    const { container } = renderButton()

    expect(container).toBeEmptyDOMElement()
  })

  it('sends a friend request and reloads the status', async () => {
    routeGets({ '/friends/status/7': { status: 'none' } })
    mockPost.mockResolvedValue({ data: {} })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Add Friend' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/friends/requests', { targetUserId: 7 }))
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
  })

  it('cancels the sent request to this user', async () => {
    routeGets({
      '/friends/status/7': { status: 'pending_sent' },
      '/friends/requests/sent': [{ id: 20, receiverId: 6 }, { id: 21, receiverId: 7 }],
    })
    mockDelete.mockResolvedValue({ data: {} })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Request Sent — Cancel' }))

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/friends/requests/21'))
  })

  it('accepts the incoming request from this user', async () => {
    routeGets({
      '/friends/status/7': { status: 'pending_received' },
      '/friends/requests/incoming': [{ id: 30, senderId: 7 }],
    })
    mockPost.mockResolvedValue({ data: {} })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/friends/requests/30/accept'))
  })

  it('declines the incoming request from this user', async () => {
    routeGets({
      '/friends/status/7': { status: 'pending_received' },
      '/friends/requests/incoming': [{ id: 30, senderId: 7 }],
    })
    mockPost.mockResolvedValue({ data: {} })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Decline' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/friends/requests/30/reject'))
  })

  it('does nothing when the request is already gone', async () => {
    routeGets({
      '/friends/status/7': { status: 'pending_received' },
      '/friends/requests/incoming': [],
    })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(mockGet).toHaveBeenCalledWith('/friends/status/7'))
    await waitFor(() => expect(mockGet.mock.calls.filter(([url]) => url === '/friends/status/7')).toHaveLength(2))
    expect(mockPost).not.toHaveBeenCalled()
    expect(mockShowSnackbar).not.toHaveBeenCalled()
  })

  it('removes a friend after confirmation', async () => {
    routeGets({ '/friends/status/7': { status: 'friends' } })
    mockDelete.mockResolvedValue({ data: {} })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Remove Friend' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Remove this user from your friends?')).toBeInTheDocument()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/friends/7'))
  })

  it('keeps the friend when the removal is cancelled', async () => {
    routeGets({ '/friends/status/7': { status: 'friends' } })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Remove Friend' }))
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('shows the API error when an action fails', async () => {
    routeGets({ '/friends/status/7': { status: 'none' } })
    mockPost.mockRejectedValue({ response: { data: { message: 'Already friends' } } })
    renderButton()

    fireEvent.click(await screen.findByRole('button', { name: 'Add Friend' }))

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Already friends', 'error'))
    expect(screen.getByRole('button', { name: 'Add Friend' })).toBeEnabled()
  })

  it('offers a retry when the status cannot be loaded', async () => {
    routeGets({ '/friends/status/7': new Error('down') })
    renderButton()

    expect(await screen.findByRole('button', { name: "Couldn't load friend status — Retry" })).toBeInTheDocument()
  })
})
