import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

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
const mockDelete = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: (...a) => mockPost(...a),
    delete: (...a) => mockDelete(...a),
  },
  getApiRoot: () => '',
}))

import Friends from '../pages/Friends'

const incoming = [{ id: 11, senderId: 5, senderUsername: 'alice' }]
const sent = [{ id: 12, receiverId: 6, receiverUsername: 'bob' }]
const friends = [{ userId: 7, username: 'carol', level: 3, totalXp: 1234, streak: 2 }]

function setupApi({ incomingList = incoming, sentList = sent, friendList = friends } = {}) {
  mockGet.mockImplementation((url) => {
    if (url === '/friends/requests/incoming') return Promise.resolve({ data: incomingList })
    if (url === '/friends/requests/sent') return Promise.resolve({ data: sentList })
    if (url === '/friends') return Promise.resolve({ data: friendList })
    return Promise.reject(new Error('unexpected ' + url))
  })
}

function renderPage() {
  return render(<MemoryRouter><Friends /></MemoryRouter>)
}

describe('Friends page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setupApi()
  })

  it('lists incoming requests, sent requests and friends with their stats', async () => {
    renderPage()

    expect(await screen.findByText('alice')).toHaveAttribute('href', '/users/5')
    expect(screen.getByText('bob')).toHaveAttribute('href', '/users/6')
    expect(screen.getByText('Pending')).toBeInTheDocument()
    expect(screen.getByText('carol')).toHaveAttribute('href', '/users/7')
    expect(screen.getByText(`Level 3 · ${(1234).toLocaleString()} XP · Streak: 2`)).toBeInTheDocument()
    expect(screen.getByText('Incoming Requests')).toBeInTheDocument()
    expect(screen.getByText('Sent Requests')).toBeInTheDocument()
  })

  it('shows the empty state and hides request sections when there is nothing', async () => {
    setupApi({ incomingList: [], sentList: [], friendList: [] })
    renderPage()

    expect(await screen.findByText('No friends yet. Send a request above!')).toBeInTheDocument()
    expect(screen.queryByText('Incoming Requests')).not.toBeInTheDocument()
    expect(screen.queryByText('Sent Requests')).not.toBeInTheDocument()
  })

  it('shows a snackbar when loading fails', async () => {
    mockGet.mockRejectedValue(new Error('boom'))
    renderPage()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Failed to load friends data.', 'error'))
  })

  it('sends a trimmed username on Enter, clears the field and reloads', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('carol')
    const field = screen.getByLabelText('Username')

    expect(screen.getByRole('button', { name: 'Send Request' })).toBeDisabled()
    fireEvent.change(field, { target: { value: '  dave ' } })
    fireEvent.keyDown(field, { key: 'Enter' })

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/friends/requests/by-username', { username: 'dave' }))
    await waitFor(() => expect(field).toHaveValue(''))
    expect(mockShowSnackbar).toHaveBeenCalledWith('Friend request sent to dave.')
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(6))
  })

  it('shows the API error when sending fails', async () => {
    mockPost.mockRejectedValue({ response: { data: { message: 'No such user' } } })
    renderPage()
    await screen.findByText('carol')

    fireEvent.change(screen.getByLabelText('Username'), { target: { value: 'ghost' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send Request' }))

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('No such user', 'error'))
  })

  it('accepts an incoming request and reloads the lists', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/friends/requests/11/accept'))
    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('You are now friends with alice.'))
    expect(mockGet).toHaveBeenCalledTimes(6)
  })

  it('disables both buttons of a request while it is in flight', async () => {
    let finish
    mockPost.mockReturnValue(new Promise(resolve => { finish = resolve }))
    renderPage()
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Decline' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Accept' })).toBeDisabled())
    expect(screen.getByRole('button', { name: 'Decline' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancel' })).not.toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))
    expect(mockPost).toHaveBeenCalledTimes(1)
    expect(mockPost).toHaveBeenCalledWith('/friends/requests/11/reject')
    finish({ data: {} })
    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Friend request declined.'))
  })

  it('cancels a sent request', async () => {
    mockDelete.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('bob')

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/friends/requests/12'))
    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Friend request cancelled.'))
  })

  it('removes a friend only after confirming', async () => {
    mockDelete.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('carol')

    fireEvent.click(screen.getByRole('button', { name: 'Remove' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Remove carol from your friends?')).toBeInTheDocument()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/friends/7'))
    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Removed carol from friends.'))
  })

  it('does not remove a friend when the confirmation is cancelled', async () => {
    renderPage()
    await screen.findByText('carol')

    fireEvent.click(screen.getByRole('button', { name: 'Remove' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('shows the fallback error when an action fails', async () => {
    mockPost.mockRejectedValue(new Error('boom'))
    renderPage()
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Failed to accept request.', 'error'))
  })
})
