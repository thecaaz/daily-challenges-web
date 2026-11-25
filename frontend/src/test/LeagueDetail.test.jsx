import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

// Stable identities, like the real (memoized) providers.
const mockAuthValue = { user: { id: 1, username: 'me' }, loading: false, fetchMe: vi.fn() }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuthValue,
}))
const mockSnackbarValue = { showSnackbar: vi.fn() }
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => mockSnackbarValue,
}))

const mockGet = vi.fn()
const mockPost = vi.fn()
const mockDelete = vi.fn()
const mockPatch = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: (...a) => mockPost(...a),
    delete: (...a) => mockDelete(...a),
    patch: (...a) => mockPatch(...a),
  },
  getApiRoot: () => '',
}))

import LeagueDetail from '../pages/LeagueDetail'

const league = {
  id: 7, name: 'Pals', ownerId: 2, ownerUsername: 'boss',
  members: [
    { userId: 2, username: 'boss', role: 'owner', level: 1, streak: 0 },
    { userId: 1, username: 'me', role: 'member', level: 1, streak: 0 },
  ],
}

function setupApi(leaderboard) {
  mockGet.mockImplementation((url) => {
    if (url === '/leagues/7') return Promise.resolve({ data: league })
    if (url === '/games') return Promise.resolve({ data: [{ id: 3, name: 'Wordle', currentScoringDay: '2026-11-03' }] })
    if (url === '/leagues/7/game-summaries') return Promise.resolve({ data: { items: [{ gameId: 3, gameName: 'Wordle', playCount: 2 }], totalCount: 1 } })
    if (url === '/leagues/7/leaderboard') return Promise.resolve({ data: leaderboard })
    return Promise.reject(new Error('unexpected ' + url))
  })
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/leagues/7']}>
      <Routes>
        <Route path="/leagues/:id" element={<LeagueDetail />} />
        <Route path="/leagues" element={<div>Leagues list page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

const ownedLeague = {
  id: 7, name: 'Pals', ownerId: 1, ownerUsername: 'me', memberCount: 2,
  members: [
    { userId: 1, username: 'me', role: 'owner', level: 3, streak: 4 },
    { userId: 2, username: 'pal', role: 'member', level: 1, streak: 0 },
  ],
  pendingInvitations: [
    { id: 11, inviteeUsername: 'newbie', inviterUsername: 'me' },
    { id: 12, inviteeUsername: null, inviterUsername: 'me' },
  ],
}

function setupOwnedLeague(overrides = {}) {
  const current = { ...ownedLeague, ...overrides }
  mockGet.mockImplementation((url) => {
    if (url === '/leagues/7') return Promise.resolve({ data: current })
    if (url === '/games') return Promise.resolve({ data: [{ id: 3, name: 'Wordle', currentScoringDay: '2026-11-03' }] })
    if (url === '/leagues/7/game-summaries') return Promise.resolve({ data: { items: [], totalCount: 0 } })
    return Promise.reject(new Error('unexpected ' + url))
  })
}

function leagueFetchCount() {
  return mockGet.mock.calls.filter(c => c[0] === '/leagues/7').length
}

describe('LeagueDetail page', () => {
  beforeEach(() => {
    mockGet.mockReset()
    mockPost.mockReset()
    mockDelete.mockReset()
    mockPatch.mockReset()
    mockSnackbarValue.showSnackbar.mockReset()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  it('shows owner actions and the invitations tab to the owner', async () => {
    setupOwnedLeague()

    renderPage()

    expect(await screen.findByRole('button', { name: 'Invite' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create invite link' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Rename' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Invitations' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Leave' })).toBeNull()
    expect(screen.getByText('2 members · Owner: me')).toBeInTheDocument()
  })

  it('shows only the Leave action to a member', async () => {
    setupApi({ entries: [] })

    renderPage()

    expect(await screen.findByRole('button', { name: 'Leave' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Invite' })).toBeNull()
    expect(screen.queryByRole('tab', { name: 'Invitations' })).toBeNull()
  })

  it('redirects to the league list when the league is not found', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/leagues/7') return Promise.reject({ response: { status: 404 } })
      return Promise.resolve({ data: [] })
    })

    renderPage()

    expect(await screen.findByText('Leagues list page')).toBeInTheDocument()
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('League not found.', 'error')
  })

  it('shows the empty games message for the selected period', async () => {
    setupOwnedLeague()

    renderPage()

    expect(await screen.findByText('No games played in this league in the last 7 days.')).toBeInTheDocument()
  })

  it('lets the owner remove another member but not themselves', async () => {
    setupOwnedLeague()
    mockDelete.mockResolvedValue({})
    renderPage()
    fireEvent.click(await screen.findByRole('tab', { name: 'Members' }))

    const removeButtons = screen.getAllByRole('button', { name: 'Remove member' })
    expect(removeButtons).toHaveLength(1)
    fireEvent.click(removeButtons[0])

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/leagues/7/members/2'))
    expect(window.confirm).toHaveBeenCalledWith('Remove pal from this league?')
    await waitFor(() => expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('pal removed.'))
    expect(screen.getByText('Level 3 · Streak 4')).toBeInTheDocument()
  })

  it('sends an invitation by username and refreshes the league', async () => {
    setupOwnedLeague()
    mockPost.mockResolvedValue({})
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Invite' }))
    const fetchesBefore = leagueFetchCount()

    fireEvent.change(screen.getByLabelText('Username'), { target: { value: '  newpal ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send Invite' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/leagues/7/invitations', { username: 'newpal' }))
    await waitFor(() => expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Invitation sent to newpal.'))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(leagueFetchCount()).toBe(fetchesBefore + 1)
  })

  it('shows a created invite link in a dialog', async () => {
    setupOwnedLeague()
    mockPost.mockResolvedValue({ data: { token: 'abc123' } })
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Create invite link' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByDisplayValue(`${window.location.origin}/leagues/join/abc123`)).toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledWith('/leagues/7/invite-link')
  })

  it('renames the league and updates the header', async () => {
    setupOwnedLeague()
    mockPatch.mockResolvedValue({ data: { name: 'Buddies' } })
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Rename' }))
    const input = screen.getByLabelText('New name')
    expect(input).toHaveValue('Pals')

    fireEvent.change(input, { target: { value: 'Buddies ' } })
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Rename' }))

    await waitFor(() => expect(mockPatch).toHaveBeenCalledWith('/leagues/7/name', { name: 'Buddies' }))
    expect(await screen.findByRole('heading', { name: 'Buddies' })).toBeInTheDocument()
  })

  it('deletes the league after confirmation and returns to the list', async () => {
    setupOwnedLeague()
    mockDelete.mockResolvedValue({})
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete' }))

    expect(await screen.findByText('Leagues list page')).toBeInTheDocument()
    expect(window.confirm).toHaveBeenCalledWith('Delete this league? This cannot be undone.')
    expect(mockDelete).toHaveBeenCalledWith('/leagues/7')
  })

  it('does nothing when leaving is not confirmed', async () => {
    setupApi({ entries: [] })
    window.confirm.mockReturnValue(false)
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Leave' }))

    expect(mockPost).not.toHaveBeenCalled()
  })

  it('lists pending invitations and cancels one', async () => {
    setupOwnedLeague()
    mockDelete.mockResolvedValue({})
    renderPage()
    fireEvent.click(await screen.findByRole('tab', { name: 'Invitations' }))

    expect(screen.getByText('newbie')).toBeInTheDocument()
    expect(screen.getByText('Link invite')).toBeInTheDocument()
    fireEvent.click(screen.getAllByRole('button', { name: 'Cancel invitation' })[0])

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/leagues/invitations/11'))
    await waitFor(() => expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Invitation cancelled.'))
  })

  it('asks to pick a game on the leaderboard tab until one is selected', async () => {
    setupOwnedLeague()
    renderPage()

    fireEvent.click(await screen.findByRole('tab', { name: 'Leaderboard' }))

    expect(screen.getByText('Select a game above, or pick one from the Games tab.')).toBeInTheDocument()
    expect(mockGet.mock.calls.some(c => c[0] === '/leagues/7/leaderboard')).toBe(false)
  })
})

describe('LeagueDetail leaderboard', () => {
  beforeEach(() => { mockGet.mockReset() })

  it("defaults to the game's current scoring day and renders masked scores", async () => {
    setupApi({
      scoringDay: '2026-11-03', scoresHidden: true,
      entries: [{ rank: 1, userId: 2, username: 'boss' }],
    })
    renderPage()
    fireEvent.click(await screen.findByText('2 plays'))

    await waitFor(() => expect(screen.getByText('Hidden')).toBeInTheDocument())
    const call = mockGet.mock.calls.find(c => c[0] === '/leagues/7/leaderboard')
    expect(call[1].params).toEqual({ gameId: 3, scoringDay: '2026-11-03' })
    expect(screen.getByText(/hidden until you submit/i)).toBeInTheDocument()
    expect(screen.queryByRole('img')).toBeNull()
  })
})

import Leagues from '../pages/Leagues'

describe('Leagues list', () => {
  beforeEach(() => { mockGet.mockReset() })

  it('shows the Owner chip only on leagues the current user owns', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/leagues') return Promise.resolve({ data: [
        { id: 1, name: 'Mine', ownerId: 1, ownerUsername: 'me', memberCount: 2 },
        { id: 2, name: 'Theirs', ownerId: 2, ownerUsername: 'boss', memberCount: 3 },
      ] })
      if (url === '/leagues/invitations/pending') return Promise.resolve({ data: [] })
      return Promise.reject(new Error('unexpected ' + url))
    })
    render(<MemoryRouter><Leagues /></MemoryRouter>)
    await screen.findByText('Mine')
    expect(screen.getAllByText('Owner')).toHaveLength(1)
    expect(screen.getByText('Mine').closest('a')).toHaveTextContent('Owner')
  })
})
