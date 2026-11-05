import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
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
vi.mock('../api', () => ({
  default: { get: (...a) => mockGet(...a), post: vi.fn(), delete: vi.fn(), patch: vi.fn() },
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
      <Routes><Route path="/leagues/:id" element={<LeagueDetail />} /></Routes>
    </MemoryRouter>
  )
}

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
