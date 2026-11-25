import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const mockAuthValue = { user: { id: 1, username: 'me' }, loading: false, fetchMe: vi.fn() }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuthValue,
}))
const mockShowSnackbar = vi.fn()
const mockSnackbarValue = { showSnackbar: mockShowSnackbar }
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => mockSnackbarValue,
}))

const mockGet = vi.fn()
const mockPost = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: (...a) => mockPost(...a),
  },
  getApiRoot: () => '',
}))

import Leagues from '../pages/Leagues'

const leagues = [
  { id: 7, name: 'Pals', memberCount: 1, ownerId: 1, ownerUsername: 'me' },
  { id: 8, name: 'Work', memberCount: 4, ownerId: 2, ownerUsername: 'boss' },
]
const invitations = [{ id: 21, leagueId: 9, leagueName: 'Rivals', inviterUsername: 'eve' }]

function setupApi({ leagueList = leagues, invitationList = invitations } = {}) {
  mockGet.mockImplementation((url) => {
    if (url === '/leagues') return Promise.resolve({ data: leagueList })
    if (url === '/leagues/invitations/pending') return Promise.resolve({ data: invitationList })
    return Promise.reject(new Error('unexpected ' + url))
  })
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/leagues']}>
      <Routes>
        <Route path="/leagues" element={<Leagues />} />
        <Route path="/leagues/:id" element={<div>League detail page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('Leagues page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setupApi()
  })

  it('lists leagues with member counts and marks the ones the user owns', async () => {
    renderPage()

    const pals = (await screen.findByText('Pals')).closest('a')
    expect(pals).toHaveAttribute('href', '/leagues/7')
    expect(within(pals).getByText('1 member · Owner: me')).toBeInTheDocument()
    expect(within(pals).getByText('Owner')).toBeInTheDocument()
    const work = screen.getByText('Work').closest('a')
    expect(within(work).getByText('4 members · Owner: boss')).toBeInTheDocument()
    expect(within(work).queryByText('Owner')).not.toBeInTheDocument()
  })

  it('shows the empty state without leagues or invitations', async () => {
    setupApi({ leagueList: [], invitationList: [] })
    renderPage()

    expect(await screen.findByText('You are not in any leagues yet. Create one or ask a friend to invite you.')).toBeInTheDocument()
    expect(screen.queryByText('Pending Invitations')).not.toBeInTheDocument()
  })

  it('accepts an invitation and opens the league', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderPage()
    expect(await screen.findByText('Invited by eve')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))

    expect(await screen.findByText('League detail page')).toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledWith('/leagues/invitations/21/accept')
    expect(mockShowSnackbar).toHaveBeenCalledWith('You joined "Rivals"!')
  })

  it('refreshes the list when accepting fails', async () => {
    mockPost.mockRejectedValue(new Error('boom'))
    renderPage()
    await screen.findByText('Rivals')

    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Failed to accept invitation.', 'error'))
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(4))
  })

  it('declines an invitation, disabling both buttons meanwhile, then refreshes', async () => {
    let finish
    mockPost.mockReturnValue(new Promise(resolve => { finish = resolve }))
    renderPage()
    await screen.findByText('Rivals')

    fireEvent.click(screen.getByRole('button', { name: 'Decline' }))

    expect(screen.getByRole('button', { name: 'Accept' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Decline' })).toBeDisabled()
    finish({ data: {} })
    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Invitation declined.'))
    expect(mockPost).toHaveBeenCalledWith('/leagues/invitations/21/decline')
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(4))
  })

  it('creates a league with a trimmed name and opens it', async () => {
    mockPost.mockResolvedValue({ data: { id: 30, name: 'New Crew' } })
    renderPage()
    await screen.findByText('Pals')

    fireEvent.click(screen.getByRole('button', { name: 'New League' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('button', { name: 'Create' })).toBeDisabled()
    fireEvent.change(within(dialog).getByLabelText('League name'), { target: { value: ' New Crew ' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect(await screen.findByText('League detail page')).toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledWith('/leagues', { name: 'New Crew' })
    expect(mockShowSnackbar).toHaveBeenCalledWith('League "New Crew" created!')
  })

  it('keeps the dialog open and shows the error when creating fails', async () => {
    mockPost.mockRejectedValue({ response: { data: { message: 'Name taken' } } })
    renderPage()
    await screen.findByText('Pals')

    fireEvent.click(screen.getByRole('button', { name: 'New League' }))
    const dialog = await screen.findByRole('dialog')
    const field = within(dialog).getByLabelText('League name')
    fireEvent.change(field, { target: { value: 'Pals' } })
    fireEvent.keyDown(field, { key: 'Enter' })

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Name taken', 'error'))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(field).toHaveValue('Pals')
  })

  it('closes the create dialog on Cancel', async () => {
    renderPage()
    await screen.findByText('Pals')

    fireEvent.click(screen.getByRole('button', { name: 'New League' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})
