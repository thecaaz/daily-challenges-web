import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const admin = { id: 1, username: 'root', isAdmin: true }
const mockAuthValue = { user: admin, loading: false }
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

import AdminUsers from '../pages/AdminUsers'

const bob = { id: 2, username: 'bob', totalXp: 1500, level: 4, xpToNextLevel: 50, streak: 3, isAdmin: false }
const otherAdmin = { id: 3, username: 'eve', totalXp: 0, level: 1, xpToNextLevel: 100, streak: 0, isAdmin: true }
const self = { id: 1, username: 'root', totalXp: 0, level: 1, xpToNextLevel: 100, streak: 0, isAdmin: false }

function usersPage(items, totalCount = items.length) {
  return { data: { items, totalCount } }
}

function setupUsers(items = [bob, otherAdmin, self], totalCount) {
  mockGet.mockImplementation((url) => {
    if (url === '/admin/users') return Promise.resolve(usersPage(items, totalCount))
    if (url.endsWith('/xp-events')) return Promise.resolve({ data: { items: [], totalCount: 0 } })
    return Promise.reject(new Error('unexpected ' + url))
  })
}

function renderPage() {
  return render(<MemoryRouter><AdminUsers /></MemoryRouter>)
}

function rowFor(username) {
  return screen.getByText(username).closest('tr')
}

describe('AdminUsers', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setupUsers()
  })

  it('shows each user with formatted XP and a profile link', async () => {
    renderPage()

    await screen.findByText('bob')
    const row = rowFor('bob')
    expect(within(row).getByText('bob')).toHaveAttribute('href', '/users/2')
    expect(within(row).getByText((1500).toLocaleString())).toBeInTheDocument()
    expect(within(row).getByText('50')).toBeInTheDocument()
  })

  it('offers Delete only for non-admins other than the current user', async () => {
    renderPage()
    await screen.findByText('bob')

    expect(within(rowFor('bob')).getByRole('button', { name: 'Delete' })).toBeInTheDocument()
    expect(within(rowFor('eve')).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
    expect(within(rowFor('root')).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
  })

  it('shows "No users found." for an empty result', async () => {
    setupUsers([])
    renderPage()

    expect(await screen.findByText('No users found.')).toBeInTheDocument()
  })

  it('shows the load error in the table and a snackbar', async () => {
    mockGet.mockRejectedValue(new Error('boom'))
    renderPage()

    expect(await screen.findByText('Failed to load users.')).toBeInTheDocument()
    expect(mockShowSnackbar).toHaveBeenCalledWith('Failed to load users', 'error')
  })

  it('pages through users and keeps the total while the next page loads', async () => {
    setupUsers([bob], 120)
    renderPage()
    await screen.findByText('bob')
    expect(screen.getByText('1–50 of 120')).toBeInTheDocument()

    let finish
    mockGet.mockReturnValueOnce(new Promise(resolve => { finish = resolve }))
    fireEvent.click(screen.getByRole('button', { name: 'Go to next page' }))

    await waitFor(() => expect(mockGet).toHaveBeenLastCalledWith('/admin/users', { params: { page: 2, pageSize: 50 } }))
    expect(screen.getByText('51–100 of 120')).toBeInTheDocument()
    finish(usersPage([otherAdmin], 120))
    expect(await screen.findByText('eve')).toBeInTheDocument()
  })

  it('adds XP after asking for a reason, then clears the amount and reloads', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('bob')
    const amount = screen.getByLabelText('XP amount for bob')
    const add = within(rowFor('bob')).getByRole('button', { name: 'Add' })
    expect(add).toBeDisabled()

    fireEvent.change(amount, { target: { value: '25' } })
    fireEvent.click(add)
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Adding 25 XP')).toBeInTheDocument()
    fireEvent.change(within(dialog).getByLabelText('Reason (optional)'), { target: { value: 'bonus' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Apply' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/admin/users/2/xp', { delta: 25, reason: 'bonus' }))
    await waitFor(() => expect(amount).toHaveValue(null))
    expect(mockShowSnackbar).toHaveBeenCalledWith('XP adjusted', 'success')
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
  })

  it('deducts XP as a negative delta and does nothing when the prompt is cancelled', async () => {
    renderPage()
    await screen.findByText('bob')

    fireEvent.change(screen.getByLabelText('XP amount for bob'), { target: { value: '-7' } })
    fireEvent.click(within(rowFor('bob')).getByRole('button', { name: 'Deduct' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Deducting 7 XP')).toBeInTheDocument()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument(), { timeout: 3000 })
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('marks an invalid XP amount as an error', async () => {
    renderPage()
    await screen.findByText('bob')
    const amount = screen.getByLabelText('XP amount for bob')

    fireEvent.change(amount, { target: { value: '0' } })

    expect(amount).toHaveAttribute('aria-invalid', 'true')
    expect(within(rowFor('bob')).getByRole('button', { name: 'Add' })).toBeDisabled()
  })

  it('validates the new password length before sending it', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('bob')

    fireEvent.click(within(rowFor('bob')).getByRole('button', { name: 'Set Password' }))
    const dialog = await screen.findByRole('dialog')
    const field = within(dialog).getByLabelText('New Password')
    fireEvent.change(field, { target: { value: 'short' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Confirm' }))
    expect(within(dialog).getByText('Password must be 8–128 characters')).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()

    fireEvent.change(field, { target: { value: 'long enough' } })
    expect(within(dialog).queryByText('Password must be 8–128 characters')).not.toBeInTheDocument()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalledWith('/admin/users/2/password', { newPassword: 'long enough' }))
    expect(mockShowSnackbar).toHaveBeenCalledWith('Password updated', 'success')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument(), { timeout: 3000 })
  })

  it('shows the API error inside the password dialog', async () => {
    mockPost.mockRejectedValue({ response: { data: { message: 'Too weak' } } })
    renderPage()
    await screen.findByText('bob')

    fireEvent.click(within(rowFor('bob')).getByRole('button', { name: 'Set Password' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText('New Password'), { target: { value: 'password123' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Confirm' }))

    expect(await within(dialog).findByText('Too weak')).toBeInTheDocument()
  })

  it('deletes a user after confirmation and reloads', async () => {
    mockDelete.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('bob')

    fireEvent.click(within(rowFor('bob')).getByRole('button', { name: 'Delete' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Delete user?')).toBeInTheDocument()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/admin/users/2'))
    expect(mockShowSnackbar).toHaveBeenCalledWith('User deleted', 'success')
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
  })

  it('opens the XP audit for a user', async () => {
    renderPage()
    await screen.findByText('bob')

    fireEvent.click(within(rowFor('bob')).getByRole('button', { name: 'Audit' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/XP Audit/)).toBeInTheDocument()
    await waitFor(() => expect(mockGet).toHaveBeenCalledWith('/admin/users/2/xp-events', { params: { page: 1, pageSize: 20 } }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument(), { timeout: 3000 })
  })
})
