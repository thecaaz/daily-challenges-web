import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'

const mockAuth = { user: { id: 5, username: 'me' }, loading: false }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

// GameHeader extras that talk to the extension / favorites: not under test here.
vi.mock('../hooks/useAdapter', () => ({ default: () => null }))
vi.mock('../components/ui/FavoriteButton', () => ({ default: () => null }))

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
  getApiRoot: () => '',
}))

import GameSubmissions, { appendUniqueById } from '../pages/GameSubmissions'

const TODAY = '2026-11-04'
const YESTERDAY = '2026-11-03'

function setup({ hasSubmittedForLatest }) {
  mockGet.mockImplementation((url) => {
    if (url.startsWith('/games/1/overview')) {
      return Promise.resolve({
        data: {
          game: { id: 1, name: 'Wordle', currentScoringDay: TODAY },
          availableDates: [YESTERDAY, TODAY],
          hasSubmittedForLatest,
        },
      })
    }
    if (url.includes('today-submitters')) {
      return Promise.resolve({ data: { count: 0, usernames: [] } })
    }
    if (url.startsWith('/submissions/game/1')) {
      return Promise.resolve({
        data: {
          items: [{ id: 11, username: 'alice', userId: 2, score: '42', rank: 1, scoringDay: YESTERDAY, createdAt: '2026-11-03T10:00:00Z' }],
          page: 1,
          totalPages: 1,
        },
      })
    }
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

function renderAt(path) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/games/:gameId" element={<GameSubmissions />} />
      </Routes>
    </MemoryRouter>
  )
}

const listCalls = () => mockGet.mock.calls.map(c => c[0]).filter(u => u.startsWith('/submissions/game/1?'))

describe('GameSubmissions hidden scores', () => {
  beforeEach(() => {
    mockGet.mockReset()
    mockAuth.user = { id: 5, username: 'me' }
  })

  it("lets an admin see today's list without submitting", async () => {
    mockAuth.user = { id: 5, username: 'me', isAdmin: true }
    setup({ hasSubmittedForLatest: false })
    renderAt('/games/1')

    await waitFor(() => expect(screen.getByText('alice')).toBeInTheDocument())
    expect(listCalls()).toEqual([`/submissions/game/1?page=1&pageSize=50&scoringDay=${TODAY}`])
  })

  it('Load more skips items already shown', async () => {
    const item = (id, username) => ({ id, username, userId: id, score: String(id), rank: id, scoringDay: YESTERDAY, createdAt: '2026-11-03T10:00:00Z' })
    mockGet.mockImplementation((url) => {
      if (url.startsWith('/games/1/overview')) {
        return Promise.resolve({ data: { game: { id: 1, name: 'Wordle', currentScoringDay: TODAY }, availableDates: [YESTERDAY, TODAY], hasSubmittedForLatest: true } })
      }
      if (url.includes('today-submitters')) return Promise.resolve({ data: { count: 0, usernames: [] } })
      if (url.includes('page=1&')) return Promise.resolve({ data: { items: [item(11, 'alice'), item(12, 'bob')], page: 1, totalPages: 2 } })
      // A new submission shifted the rows: bob shows up again on page 2.
      if (url.includes('page=2&')) return Promise.resolve({ data: { items: [item(12, 'bob'), item(13, 'carol')], page: 2, totalPages: 2 } })
      return Promise.reject(new Error(`unexpected GET ${url}`))
    })
    renderAt(`/games/1?scoringDay=${YESTERDAY}`)

    await waitFor(() => expect(screen.getByText('bob')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: 'Load more' }))
    await waitFor(() => expect(screen.getByText('carol')).toBeInTheDocument())
    expect(screen.getAllByText('bob')).toHaveLength(1)
  })

  it("does not request today's list when the user hasn't submitted", async () => {
    setup({ hasSubmittedForLatest: false })
    renderAt('/games/1')

    await waitFor(() => expect(screen.getByText(/scores are hidden/i)).toBeInTheDocument())
    // Give any pending effects a chance to run.
    await new Promise(r => setTimeout(r, 20))
    expect(listCalls()).toEqual([])
  })

  it("requests today's list once the user has submitted", async () => {
    setup({ hasSubmittedForLatest: true })
    renderAt('/games/1')

    await waitFor(() => expect(screen.getByText('alice')).toBeInTheDocument())
    expect(listCalls()).toEqual([`/submissions/game/1?page=1&pageSize=50&scoringDay=${TODAY}`])
    expect(screen.queryByText(/scores are hidden/i)).not.toBeInTheDocument()
  })

  it('still loads a past day for a non-submitter', async () => {
    setup({ hasSubmittedForLatest: false })
    renderAt(`/games/1?scoringDay=${YESTERDAY}`)

    await waitFor(() => expect(screen.getByText('alice')).toBeInTheDocument())
    expect(listCalls()).toEqual([`/submissions/game/1?page=1&pageSize=50&scoringDay=${YESTERDAY}`])
  })

  it('scoringDay=all loads every day without a scoringDay filter', async () => {
    setup({ hasSubmittedForLatest: true })
    renderAt('/games/1?scoringDay=all')

    await waitFor(() => expect(screen.getByText('alice')).toBeInTheDocument())
    expect(listCalls()).toEqual(['/submissions/game/1?page=1&pageSize=50'])
  })

  it('renders masked items without score, rank or screenshot', async () => {
    mockGet.mockImplementation((url) => {
      if (url.startsWith('/games/1/overview')) {
        return Promise.resolve({ data: { game: { id: 1, name: 'Wordle', currentScoringDay: TODAY }, availableDates: [TODAY], hasSubmittedForLatest: true } })
      }
      if (url.includes('today-submitters')) return Promise.resolve({ data: { count: 1, usernames: ['bob'] } })
      return Promise.resolve({
        data: { items: [{ id: 12, username: 'bob', userId: 3, createdAt: '2026-11-04T08:00:00Z' }], page: 1, totalPages: 1, scoresHidden: true },
      })
    })
    renderAt('/games/1')

    await waitFor(() => expect(screen.getByText('bob', { selector: '[role="link"]' })).toBeInTheDocument())
    expect(screen.getByText('Hidden')).toBeInTheDocument()
    expect(screen.queryByAltText('screenshot')).not.toBeInTheDocument()
    expect(screen.queryByText(/^#/)).not.toBeInTheDocument()
  })
})

describe('appendUniqueById', () => {
  it('appends only items whose id is not shown yet', () => {
    const prev = [{ id: 1 }, { id: 2 }]
    expect(appendUniqueById(prev, [{ id: 2 }, { id: 3 }, { id: 3 }])).toEqual([{ id: 1 }, { id: 2 }, { id: 3 }])
    expect(appendUniqueById(prev, [{ id: 1 }])).toBe(prev)
    expect(appendUniqueById(prev, undefined)).toBe(prev)
  })
})
