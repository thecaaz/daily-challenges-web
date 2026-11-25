import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter, Routes, Route, useParams } from 'react-router-dom'

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

import GameSubmissions from '../pages/GameSubmissions'
import { appendUniqueById } from '../hooks/useSubmissionPages'

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
        <Route path="/compare/:a/:b" element={<CompareTarget />} />
      </Routes>
    </MemoryRouter>
  )
}

function CompareTarget() {
  const { a, b } = useParams()
  return <div>Comparing {a} and {b}</div>
}

const overview = (hasSubmittedForLatest = true) => ({
  data: { game: { id: 1, name: 'Wordle', currentScoringDay: TODAY }, availableDates: [YESTERDAY, TODAY], hasSubmittedForLatest },
})

function setupList(listResponse) {
  mockGet.mockImplementation((url) => {
    if (url.startsWith('/games/1/overview')) return Promise.resolve(overview())
    if (url.includes('today-submitters')) return Promise.resolve({ data: { count: 0, usernames: [] } })
    if (url.startsWith('/submissions/game/1')) return listResponse()
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
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

describe('GameSubmissions page states', () => {
  beforeEach(() => {
    mockGet.mockReset()
    mockAuth.user = { id: 5, username: 'me' }
  })

  it('offers a retry when the game fails to load', async () => {
    mockGet.mockRejectedValueOnce(new Error('boom'))
    setup({ hasSubmittedForLatest: true })
    renderAt('/games/1')

    fireEvent.click(await screen.findByRole('button', { name: 'Retry' }))

    await waitFor(() => expect(screen.getByText('alice')).toBeInTheDocument())
  })

  it('shows not found when the game does not exist', async () => {
    mockGet.mockRejectedValue({ response: { status: 404 } })
    renderAt('/games/1')

    expect(await screen.findByText('Game not found')).toBeInTheDocument()
  })

  it('shows not found when the submissions list returns 404', async () => {
    setupList(() => Promise.reject({ response: { status: 404 } }))
    renderAt('/games/1')

    expect(await screen.findByText('Game not found')).toBeInTheDocument()
  })

  it('offers a retry when the submissions fail to load', async () => {
    let failing = true
    setupList(() => failing
      ? Promise.reject(new Error('boom'))
      : Promise.resolve({ data: { items: [{ id: 11, username: 'alice', userId: 2, score: '42', rank: 1 }], page: 1, totalPages: 1 } }))
    renderAt('/games/1')

    expect(await screen.findByText('Failed to load submissions.')).toBeInTheDocument()
    failing = false
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))

    await waitFor(() => expect(screen.getByText('alice')).toBeInTheDocument())
    expect(screen.queryByText('Failed to load submissions.')).not.toBeInTheDocument()
  })

  it('says so when a day has no submissions', async () => {
    setupList(() => Promise.resolve({ data: { items: [], page: 1, totalPages: 1 } }))
    renderAt('/games/1')

    await waitFor(() => expect(listCalls()).toHaveLength(1))
    await waitFor(() => expect(screen.getByText('No submissions for this day yet.')).toBeInTheDocument())
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('disables submitting once the user has submitted for today', async () => {
    setup({ hasSubmittedForLatest: true })
    renderAt('/games/1')

    await screen.findByText('alice')

    expect(screen.getByLabelText('Already submitted today')).toHaveTextContent('Submitted')
    expect(screen.queryByRole('link', { name: 'Submit score' })).not.toBeInTheDocument()
  })

  it('links to the submit page for a past day, keeping the query string', async () => {
    setup({ hasSubmittedForLatest: true })
    renderAt(`/games/1?scoringDay=${YESTERDAY}`)

    await screen.findByText('alice')

    expect(screen.getByRole('link', { name: 'Submit score' })).toHaveAttribute('href', `/submit/1?scoringDay=${YESTERDAY}`)
  })

  it('compares two selected submissions', async () => {
    const shot = (id, username) => ({ id, username, userId: id, score: String(id), rank: 1, screenshotUrl: `/s/${id}.png`, createdAt: '2026-11-03T10:00:00Z' })
    setupList(() => Promise.resolve({ data: { items: [shot(11, 'alice'), shot(12, 'bob'), shot(13, 'carol')], page: 1, totalPages: 1 } }))
    renderAt('/games/1')
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Compare' }))
    const checkboxes = screen.getAllByRole('checkbox')
    fireEvent.click(checkboxes[0])
    fireEvent.click(checkboxes[1])
    fireEvent.click(checkboxes[2])
    fireEvent.click(screen.getByLabelText('Compare selected submissions'))

    expect(await screen.findByText('Comparing 12 and 13')).toBeInTheDocument()
  })

  it('leaves compare mode and clears the selection on cancel', async () => {
    const shot = (id, username) => ({ id, username, userId: id, score: String(id), rank: 1, screenshotUrl: `/s/${id}.png`, createdAt: '2026-11-03T10:00:00Z' })
    setupList(() => Promise.resolve({ data: { items: [shot(11, 'alice'), shot(12, 'bob')], page: 1, totalPages: 1 } }))
    renderAt('/games/1')
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Compare' }))
    screen.getAllByRole('checkbox').forEach(box => fireEvent.click(box))
    fireEvent.click(screen.getByRole('button', { name: 'Cancel Compare' }))

    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Compare selected submissions')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Compare' }))
    expect(screen.getAllByRole('checkbox').every(box => !box.checked)).toBe(true)
  })

  it('does not offer compare with fewer than two screenshots', async () => {
    setup({ hasSubmittedForLatest: true })
    renderAt('/games/1')

    await screen.findByText('alice')

    expect(screen.queryByRole('button', { name: 'Compare' })).not.toBeInTheDocument()
  })

  it("shows the hidden-scores card on 'all days' for a non-submitter", async () => {
    setup({ hasSubmittedForLatest: false })
    renderAt('/games/1?scoringDay=all')

    await screen.findByText('alice')

    expect(screen.getByText(/scores are hidden/i)).toBeInTheDocument()
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
