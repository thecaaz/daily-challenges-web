import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

let mockAuthValue = { user: { id: 1, username: 'me' }, loading: false, fetchMe: vi.fn() }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuthValue,
}))
const mockShowSnackbar = vi.fn()
const mockSnackbarValue = { showSnackbar: mockShowSnackbar }
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => mockSnackbarValue,
}))

const mockPost = vi.fn()
vi.mock('../api', () => ({
  default: { post: (...a) => mockPost(...a), get: vi.fn() },
  getApiRoot: () => '',
}))

const mockRefreshAdapters = vi.fn()
let mockExtensionDetected = false
vi.mock('../utils/adapters', () => ({
  refreshAdapters: (...a) => mockRefreshAdapters(...a),
  isExtensionDetected: () => mockExtensionDetected,
}))

import LeagueJoin from '../pages/LeagueJoin'
import Extension, { releasedVersion } from '../pages/Extension'

function renderJoin() {
  return render(
    <MemoryRouter initialEntries={['/leagues/join/abc']}>
      <Routes>
        <Route path="/leagues/join/:token" element={<LeagueJoin />} />
        <Route path="/leagues/:id" element={<div>League detail page</div>} />
        <Route path="/leagues" element={<div>Leagues list page</div>} />
        <Route path="/login" element={<div>Login page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('LeagueJoin', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAuthValue = { user: { id: 1, username: 'me' }, loading: false, fetchMe: vi.fn() }
  })

  it('waits for auth before joining', () => {
    mockAuthValue = { user: null, loading: true }
    renderJoin()

    expect(screen.getByText('Joining league…')).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('sends a logged-out user to login with a redirect back', async () => {
    mockAuthValue = { user: null, loading: false }
    renderJoin()

    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('joins the league and opens it', async () => {
    mockPost.mockResolvedValue({ data: { id: 7, name: 'Pals' } })
    renderJoin()

    expect(await screen.findByText('League detail page')).toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledWith('/leagues/join/abc')
    expect(mockPost).toHaveBeenCalledTimes(1)
    expect(mockShowSnackbar).toHaveBeenCalledWith('You joined "Pals"!')
  })

  it('shows the error state with a link back to the leagues', async () => {
    mockPost.mockRejectedValue(new Error('boom'))
    renderJoin()

    expect(await screen.findByText('Could not join league.')).toBeInTheDocument()
    expect(mockShowSnackbar).toHaveBeenCalledWith('Invalid or expired invite link.', 'error')
    fireEvent.click(screen.getByRole('button', { name: 'My Leagues' }))
    expect(await screen.findByText('Leagues list page')).toBeInTheDocument()
  })
})

describe('Extension', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockExtensionDetected = false
  })

  it('reads the released version from the last advertised update', () => {
    const updates = { addons: { x: { updates: [{ version: '0.4.0' }, { version: '0.5.0' }] } } }

    expect(releasedVersion(updates)).toBe('0.5.0')
    expect(releasedVersion({})).toBeNull()
    expect(releasedVersion(null)).toBeNull()
  })

  it('shows the installed chip once the extension answers', async () => {
    mockRefreshAdapters.mockResolvedValue()
    mockExtensionDetected = true
    render(<Extension />)

    expect(await screen.findByText('Extension already installed')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Download for Firefox' })).toHaveAttribute('href', '/extension.xpi')
    expect(screen.getByText(/Firefox only/)).toBeInTheDocument()
  })

  it('does not show the installed chip when detection fails', async () => {
    mockRefreshAdapters.mockRejectedValue(new Error('none'))
    render(<Extension />)

    await waitFor(() => expect(mockRefreshAdapters).toHaveBeenCalled())
    expect(screen.queryByText('Extension already installed')).not.toBeInTheDocument()
  })
})
