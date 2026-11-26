import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...args) => mockGet(...args) },
  getApiRoot: () => '',
}))

import SubmissionDetail from '../pages/SubmissionDetail'
import CompareSubmissions from '../pages/CompareSubmissions'
import AdminSubmissionsTable from '../components/Admin/AdminSubmissionsTable'

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

function routeGets(routes) {
  mockGet.mockImplementation(url => Promise.resolve({ data: routes[url] }))
}

const renderAt = (path, pattern, element) => render(
  <MemoryRouter initialEntries={[path]}>
    <Routes><Route path={pattern} element={element} /></Routes>
  </MemoryRouter>
)

describe('submitter name markup', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it.each([
    ['a linked', submission(5)],
    ['an unlinked', submission(5, { userId: null })],
    ['a nameless unlinked', submission(5, { userId: null, username: null })],
  ])('keeps %s submitter on the submission detail page', async (_, shown) => {
    routeGets({ '/submissions/5': shown })

    renderAt('/submission/5', '/submission/:id', <SubmissionDetail />)

    expect((await screen.findByText(/^Submission —/)).outerHTML).toMatchSnapshot()
  })

  it('keeps linked and unlinked submitters on the compare page', async () => {
    routeGets({ '/submissions/5': submission(5), '/submissions/6': submission(6, { userId: null }) })

    renderAt('/compare/5/6', '/compare/:id1/:id2', <CompareSubmissions />)

    expect((await screen.findByText('player5')).parentElement.outerHTML).toMatchSnapshot()
    expect(screen.getByText('player6').parentElement.outerHTML).toMatchSnapshot()
  })

  it('keeps linked, unlinked and nameless submitters in the admin table', () => {
    const submissions = [submission(1), submission(2, { userId: null }), submission(3, { userId: null, username: null })]

    const { container } = render(
      <MemoryRouter>
        <AdminSubmissionsTable submissions={submissions} onScoreChange={() => {}} onSave={() => {}} onDelete={() => {}} />
      </MemoryRouter>
    )

    const usernameCells = [...container.querySelectorAll('tbody tr')].map(row => row.children[1].outerHTML)
    expect(usernameCells).toMatchSnapshot()
  })
})
