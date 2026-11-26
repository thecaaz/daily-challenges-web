import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...args) => mockGet(...args) },
  getApiRoot: () => '',
}))
vi.mock('../components/ui/GameCard/GameCard', () => ({
  default: ({ game }) => <div>{`game card ${game.name}`}</div>,
}))

import Games from '../pages/Games'
import SubmissionDetail from '../pages/SubmissionDetail'
import CompareSubmissions from '../pages/CompareSubmissions'

function routeGets(routes) {
  mockGet.mockImplementation(url => {
    if (url in routes) {
      const response = routes[url]
      return response instanceof Error ? Promise.reject(response) : Promise.resolve({ data: response })
    }
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

const notFound = () => Object.assign(new Error('not found'), { response: { status: 404 } })

const renderAt = (path, pattern, element) => render(
  <MemoryRouter initialEntries={[path]}>
    <Routes><Route path={pattern} element={element} /></Routes>
  </MemoryRouter>
)

const submission = (id, overrides = {}) => ({
  id,
  userId: 100 + id,
  username: `player${id}`,
  gameId: 3,
  score: '1234',
  scoringDay: '2026-01-02',
  createdAt: '2026-01-02T10:00:00Z',
  ...overrides,
})

describe('Games page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('lists the games with their count', async () => {
    routeGets({ '/games': [{ id: 1, name: 'Wordle' }, { id: 2, name: 'Cutle' }] })

    render(<MemoryRouter><Games /></MemoryRouter>)

    expect(await screen.findByText('game card Wordle')).toBeInTheDocument()
    expect(screen.getByText('game card Cutle')).toBeInTheDocument()
    expect(screen.getByText('2 games')).toBeInTheDocument()
  })

  it('says so when there are no games, also for a non-list response', async () => {
    routeGets({ '/games': { unexpected: true } })

    render(<MemoryRouter><Games /></MemoryRouter>)

    expect(await screen.findByText('No games yet')).toBeInTheDocument()
  })

  it('offers a retry when the games cannot be loaded', async () => {
    routeGets({ '/games': new Error('down') })
    render(<MemoryRouter><Games /></MemoryRouter>)
    expect(await screen.findByText("Couldn't load the games")).toBeInTheDocument()

    routeGets({ '/games': [{ id: 1, name: 'Wordle' }] })
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('1 game')).toBeInTheDocument()
  })
})

describe('SubmissionDetail page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const renderSubmission = id => renderAt(`/submission/${id}`, '/submission/:id', <SubmissionDetail />)

  it('shows the submission with its submitter and rank', async () => {
    routeGets({ '/submissions/5': submission(5, { rank: 2 }) })

    renderSubmission(5)

    expect(await screen.findByRole('link', { name: 'player5' })).toHaveAttribute('href', '/users/105')
    expect(screen.getByText(new RegExp(`Score: ${(1234).toLocaleString()}`))).toBeInTheDocument()
    expect(screen.getByText('#2')).toBeInTheDocument()
    expect(screen.getByText('No screenshot attached.')).toBeInTheDocument()
  })

  it('says the submission was not found on a 404', async () => {
    mockGet.mockRejectedValue(notFound())

    renderSubmission(5)

    expect(await screen.findByRole('alert')).toHaveTextContent('Submission not found')
  })

  it('offers a retry when the submission cannot be loaded', async () => {
    routeGets({ '/submissions/5': new Error('down') })
    renderSubmission(5)
    expect(await screen.findByText('Failed to load this submission.')).toBeInTheDocument()

    routeGets({ '/submissions/5': submission(5) })
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByRole('link', { name: 'player5' })).toBeInTheDocument()
  })
})

describe('CompareSubmissions page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const renderComparison = () => renderAt('/compare/5/6', '/compare/:id1/:id2', <CompareSubmissions />)

  it('shows both submissions side by side', async () => {
    routeGets({ '/submissions/5': submission(5), '/submissions/6': submission(6, { score: null }) })

    renderComparison()

    expect(await screen.findByText('Compare Submissions')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'player5' })).toHaveAttribute('href', '/users/105')
    expect(screen.getByRole('link', { name: 'player6' })).toHaveAttribute('href', '/users/106')
    expect(screen.getByText((1234).toLocaleString())).toBeInTheDocument()
  })

  it('says the comparison cannot be shown when either submission fails to load', async () => {
    routeGets({ '/submissions/5': submission(5), '/submissions/6': notFound() })

    renderComparison()

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load submissions for comparison')
  })

  it('loads both submissions', async () => {
    routeGets({ '/submissions/5': submission(5), '/submissions/6': submission(6) })

    renderComparison()

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
    expect(mockGet).toHaveBeenCalledWith('/submissions/5')
    expect(mockGet).toHaveBeenCalledWith('/submissions/6')
  })
})
