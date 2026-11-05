import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const me = { id: 5, username: 'me' }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: me, loading: false }),
}))

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => ({
  ...(await importOriginal()),
  useNavigate: () => mockNavigate,
}))

const mockGet = vi.fn()
const mockPost = vi.fn()
const mockDelete = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: (...a) => mockPost(...a),
    delete: (...a) => mockDelete(...a),
  },
  getApiRoot: () => '',
}))

import NotificationBell, { notificationRoute } from '../components/NotificationBell'

describe('notificationRoute', () => {
  it.each([
    [{ type: 'friend_request', actorUserId: 9 }, '/friends'],
    [{ type: 'friend_request_accepted', actorUserId: 9 }, '/friends'],
    [{ type: 'league_invite' }, '/leagues'],
    [{ type: 'league_joined' }, '/leagues'],
    [{ type: 'achievement' }, '/users/5'],
    [{ type: 'day_win', gameId: 3, scoringDay: '2026-11-03', rank: 1 }, '/games/3?scoringDay=2026-11-03'],
    [{ type: 'day_placement', gameId: 3, scoringDay: '2026-11-03', rank: 2 }, '/games/3?scoringDay=2026-11-03'],
    [{ type: 'day_placement', gameId: 3 }, '/games/3'],
    [{ type: 'day_win' }, null],
    [{ type: 'something_new' }, null],
  ])('%j -> %s', (n, expected) => {
    expect(notificationRoute(n, me)).toBe(expected)
  })

  it('never adds a ?tab query to /friends', () => {
    expect(notificationRoute({ type: 'friend_request' }, me)).not.toContain('?')
  })
})

const notifications = [
  { id: 1, type: 'day_placement', gameId: 3, scoringDay: '2026-11-03', rank: 2, message: 'You placed 2nd', isRead: true, createdAt: '2026-11-04T08:00:00Z' },
  { id: 2, type: 'league_invite', message: 'Invited to a league', isRead: true, createdAt: '2026-11-04T08:00:00Z' },
  { id: 3, type: 'achievement', message: 'Achievement unlocked', isRead: false, createdAt: '2026-11-04T08:00:00Z' },
]

function renderBell() {
  mockGet.mockImplementation((url) => {
    if (url === '/notifications/unread-count') return Promise.resolve({ data: { unreadCount: 1 } })
    return Promise.resolve({ data: { items: notifications, unreadCount: 1 } })
  })
  mockPost.mockResolvedValue({})
  render(<MemoryRouter><NotificationBell /></MemoryRouter>)
}

async function openBell() {
  fireEvent.click(screen.getByRole('button'))
  await waitFor(() => expect(screen.getByText('Invited to a league')).toBeInTheDocument())
}

describe('NotificationBell', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('routes a league notification to /leagues', async () => {
    renderBell()
    await openBell()
    fireEvent.click(screen.getByText('Invited to a league'))
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/leagues'))
  })

  it('routes an achievement to the own profile and marks it read', async () => {
    renderBell()
    await openBell()
    fireEvent.click(screen.getByText('Achievement unlocked'))
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/users/5'))
    expect(mockPost).toHaveBeenCalledWith('/notifications/3/read')
  })

  it('shows a rank chip only when the notification has a rank', async () => {
    renderBell()
    await openBell()
    expect(screen.getByText('#2')).toBeInTheDocument()
    expect(screen.queryByText('#—')).not.toBeInTheDocument()
  })

  it('puts a notification back when deleting it fails', async () => {
    let fail
    mockDelete.mockReturnValue(new Promise((_, reject) => { fail = reject }))
    renderBell()
    await openBell()
    const removeButtons = screen.getAllByLabelText('Remove notification')
    fireEvent.click(removeButtons[1])
    expect(mockDelete).toHaveBeenCalledWith('/notifications/2')
    await waitFor(() => expect(screen.queryByText('Invited to a league')).not.toBeInTheDocument())
    fail(new Error('boom'))
    await waitFor(() => expect(screen.getByText('Invited to a league')).toBeInTheDocument())
  })
})
