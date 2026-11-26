import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material/styles'
import createAppTheme from '../theme'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...args) => mockGet(...args) },
  getApiRoot: () => '',
}))

import VersionBadge from '../components/VersionBadge'
import HiddenScoresCard from '../components/ui/HiddenScoresCard'
import RankRow from '../components/dashboard/RankRow'
import AuthForm from '../components/ui/AuthForm'

const inRouter = ui => render(<MemoryRouter>{ui}</MemoryRouter>)

describe('VersionBadge', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows the release version and opens its changelog', async () => {
    mockGet.mockResolvedValue({ data: { version: '1.30.0', rawVersion: '1.30.0+abc', changelog: '## Notes' } })

    render(<VersionBadge />)
    fireEvent.click(await screen.findByRole('button', { name: '1.30.0+abc' }))
    expect(screen.getByText('v1.30.0')).toBeInTheDocument()

    expect(mockGet).toHaveBeenCalledWith('/info')
    expect(await screen.findByText('Changelog — 1.30.0')).toBeInTheDocument()
    expect(screen.getByText('Full: 1.30.0+abc')).toBeInTheDocument()
  })

  it('keeps a version that already starts with v', async () => {
    mockGet.mockResolvedValue({ data: { version: 'v2.0.0' } })

    render(<VersionBadge />)

    expect(await screen.findByRole('button', { name: 'v2.0.0' })).toBeInTheDocument()
  })

  it('renders nothing when the release info cannot be loaded', async () => {
    mockGet.mockRejectedValue(new Error('down'))

    const { container } = render(<VersionBadge />)

    await waitFor(() => expect(mockGet).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing without a version', async () => {
    mockGet.mockResolvedValue({ data: {} })

    const { container } = render(<VersionBadge />)

    await waitFor(() => expect(mockGet).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()
  })
})

describe('HiddenScoresCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const submitters = (count, usernames) => mockGet.mockResolvedValue({ data: { count, usernames } })

  it('loads today\'s submitters of the game and links to the submit page', async () => {
    submitters(1, ['alice'])

    inRouter(<HiddenScoresCard gameId={4} search="?x=1" />)

    expect(await screen.findByText('alice has already submitted')).toBeInTheDocument()
    expect(mockGet).toHaveBeenCalledWith('/submissions/game/4/today-submitters')
    expect(screen.getByRole('link', { name: 'Submit score' })).toHaveAttribute('href', '/submit/4?x=1')
  })

  it('shows a spinner while loading', () => {
    mockGet.mockReturnValue(new Promise(() => {}))

    inRouter(<HiddenScoresCard gameId={4} />)

    expect(screen.getByRole('progressbar')).toBeInTheDocument()
  })

  it('names up to three submitters', async () => {
    submitters(3, ['a', 'b', 'c'])

    inRouter(<HiddenScoresCard gameId={4} />)

    expect(await screen.findByText('a, b and c have already submitted')).toBeInTheDocument()
  })

  it('counts more than three submitters', async () => {
    submitters(4, ['a', 'b', 'c', 'd'])

    inRouter(<HiddenScoresCard gameId={4} />)

    expect(await screen.findByText('4 players have already submitted')).toBeInTheDocument()
  })

  it('invites to be first when nobody submitted or the lookup fails', async () => {
    mockGet.mockRejectedValue(new Error('down'))

    inRouter(<HiddenScoresCard gameId={4} />)

    expect(await screen.findByText('🎯 Be the first to submit your score!')).toBeInTheDocument()
  })
})

describe('RankRow', () => {
  const rank = { gameId: 2, gameName: 'Wordle', score: 3, totalSubmissions: 9 }
  const renderRow = position => inRouter(<RankRow rank={{ ...rank, rank: position }} />)

  it.each([
    [1, '🥇 #1 / 9', 'MuiChip-colorWarning'],
    [2, '🥈 #2 / 9', 'MuiChip-colorSuccess'],
    [3, '🥉 #3 / 9', 'MuiChip-colorSuccess'],
    [4, '#4 / 9', 'MuiChip-colorDefault'],
  ])('labels rank %i as "%s" with %s', (position, label, colorClass) => {
    renderRow(position)

    expect(screen.getByText(label).closest('.MuiChip-root')).toHaveClass(colorClass)
  })

  it('links to the game and shows the score', () => {
    renderRow(1)

    expect(screen.getByRole('link', { name: 'Wordle' })).toHaveAttribute('href', '/games/2')
    expect(screen.getByText('Score: 3')).toBeInTheDocument()
  })
})

describe('AuthForm', () => {
  it.each(['light', 'dark'])('colours the alternative link with the accent in %s mode', mode => {
    render(
      <ThemeProvider theme={createAppTheme(mode)}>
        <MemoryRouter>
          <AuthForm title="Sign in" linkPrefix="New here?" linkText="Register" linkTo="/register" />
        </MemoryRouter>
      </ThemeProvider>
    )

    const link = screen.getByRole('link', { name: 'Register' })
    expect(link).toHaveAttribute('href', '/register')
    expect(link).toHaveStyle({ color: '#ff7ab6', fontWeight: 600 })
  })
})
