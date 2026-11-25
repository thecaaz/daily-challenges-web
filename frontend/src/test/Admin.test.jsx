import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'

const mockAuthValue = { user: { id: 1, username: 'root', isAdmin: true }, loading: false }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuthValue,
}))
const mockSnackbarValue = { showSnackbar: vi.fn() }
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => mockSnackbarValue,
}))

const mockGet = vi.fn()
const mockPost = vi.fn()
const mockPut = vi.fn()
const mockDelete = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: (...a) => mockPost(...a),
    put: (...a) => mockPut(...a),
    delete: (...a) => mockDelete(...a),
  },
  getApiRoot: () => '',
}))

import Admin from '../pages/Admin'

const wordle = { id: 7, name: 'Wordle', resetTime: '00:00', url: 'https://wordle.test', description: '', rankingMode: 'highest' }
const tango = { id: 8, name: 'Tango', resetTime: '00:00', rankingMode: 'lowest' }

function submission(id, username, score) {
  return { id, username, userId: id + 100, score, createdAt: '2026-11-03T10:00:00Z' }
}

function setupApi({ games = [wordle, tango], submissionsByGame = {} } = {}) {
  mockGet.mockImplementation((url) => {
    if (url === '/games') return Promise.resolve({ data: games })
    const match = /^\/submissions\/game\/(\d+)\/unfiltered$/.exec(url)
    if (match) return Promise.resolve({ data: submissionsByGame[match[1]] ?? [] })
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/admin']}>
      <Routes>
        <Route path="/admin" element={<Admin />} />
        <Route path="/" element={<div>Games overview</div>} />
        <Route path="/admin/users" element={<div>Users admin</div>} />
      </Routes>
    </MemoryRouter>
  )
}

async function confirmDialog() {
  const dialog = await screen.findByRole('dialog')
  fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }))
}

describe('Admin page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('lists games with their play link and reset time', async () => {
    setupApi()
    renderPage()

    expect(await screen.findByText('Wordle')).toBeInTheDocument()
    expect(screen.getByText('Tango')).toBeInTheDocument()
    expect(screen.getByText('(link)')).toBeInTheDocument()
  })

  it('shows a snackbar when the games fail to load', async () => {
    mockGet.mockRejectedValue(new Error('boom'))
    renderPage()

    await waitFor(() => expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Failed to load games', 'error'))
  })

  it('navigates to the user admin page', async () => {
    setupApi()
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Manage Users' }))

    expect(await screen.findByText('Users admin')).toBeInTheDocument()
  })

  it('creates a game and returns to the games overview', async () => {
    setupApi()
    mockPost.mockResolvedValue({ data: {} })
    renderPage()
    await screen.findByText('Wordle')

    fireEvent.change(screen.getByLabelText(/Game name/), { target: { value: '  Connections ' } })
    fireEvent.change(screen.getByLabelText(/Description/, { selector: 'textarea' }), { target: { value: 'Group words' } })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'lowest' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create Game' }))

    expect(await screen.findByText('Games overview')).toBeInTheDocument()
    const created = mockPost.mock.calls[0][1]
    expect(mockPost.mock.calls[0][0]).toBe('/games')
    expect(created.get('name')).toBe('Connections')
    expect(created.get('description')).toBe('Group words')
    expect(created.get('rankingMode')).toBe('lowest')
    expect(created.has('image')).toBe(false)
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Game created', 'success')
  })

  it('rejects an invalid reset time without calling the API', async () => {
    setupApi()
    const { container } = renderPage()
    await screen.findByText('Wordle')

    fireEvent.change(screen.getByLabelText(/Game name/), { target: { value: 'New' } })
    fireEvent.change(container.querySelector('input[type="time"]'), { target: { value: '' } })
    fireEvent.submit(container.querySelector('form'))

    await waitFor(() => expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Enter a valid reset time', 'error'))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows the edit form for a game and hides it on cancel', async () => {
    setupApi()
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Tango' }))

    expect(screen.getByText('Edit Game')).toBeInTheDocument()
    expect(screen.getByLabelText('Name')).toHaveValue('Tango')
    expect(screen.getAllByRole('combobox')[1]).toHaveValue('lowest')

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByText('Edit Game')).not.toBeInTheDocument()
  })

  it('saves an edited game, closes the form and reloads the games', async () => {
    setupApi()
    mockPut.mockResolvedValue({ data: {} })
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Wordle' }))

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: ' Wordle 2 ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByText('Edit Game')).not.toBeInTheDocument())
    expect(mockPut.mock.calls[0][0]).toBe('/games/7')
    expect(mockPut.mock.calls[0][1].get('name')).toBe('Wordle 2')
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Game updated', 'success')
    expect(mockGet.mock.calls.filter(c => c[0] === '/games')).toHaveLength(2)
  })

  it('deletes a game after confirmation and clears its open panels', async () => {
    setupApi({ submissionsByGame: { 7: [submission(1, 'alice', '10')] } })
    mockDelete.mockResolvedValue({})
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Wordle' }))
    fireEvent.click(screen.getAllByRole('button', { name: 'Manage Submissions' })[0])
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Delete Wordle' }))
    await confirmDialog()

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/games/7'))
    await waitFor(() => expect(screen.queryByText('Edit Game')).not.toBeInTheDocument())
    expect(screen.queryByText('Submissions')).not.toBeInTheDocument()
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Game deleted', 'success')
  })

  it('does not delete a game when the confirmation is cancelled', async () => {
    setupApi()
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Delete Wordle' }))

    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('shows the submissions of a game with links to their users', async () => {
    setupApi({ submissionsByGame: { 7: [submission(1, 'alice', '10'), { ...submission(2, 'ghost', '5'), userId: null }] } })
    renderPage()
    fireEvent.click((await screen.findAllByRole('button', { name: 'Manage Submissions' }))[0])

    expect(await screen.findByText('Submissions')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'alice' })).toHaveAttribute('href', '/users/101')
    expect(screen.queryByRole('link', { name: 'ghost' })).not.toBeInTheDocument()
    expect(screen.getByText('ghost')).toBeInTheDocument()
    expect(screen.getByDisplayValue('10')).toBeInTheDocument()
  })

  it('warns when an edited score is not a number', async () => {
    setupApi({ submissionsByGame: { 7: [submission(1, 'alice', '10')] } })
    renderPage()
    fireEvent.click((await screen.findAllByRole('button', { name: 'Manage Submissions' }))[0])

    fireEvent.change(await screen.findByDisplayValue('10'), { target: { value: 'abc' } })

    expect(screen.getByText('Not a number')).toBeInTheDocument()
  })

  it('saves an edited score trimmed and reloads the submissions', async () => {
    setupApi({ submissionsByGame: { 7: [submission(1, 'alice', '10')] } })
    mockPut.mockResolvedValue({ data: {} })
    renderPage()
    fireEvent.click((await screen.findAllByRole('button', { name: 'Manage Submissions' }))[0])

    fireEvent.change(await screen.findByDisplayValue('10'), { target: { value: ' 12 ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(mockPut).toHaveBeenCalledWith('/submissions/1', { score: '12' }))
    await waitFor(() => expect(mockGet.mock.calls.filter(c => c[0] === '/submissions/game/7/unfiltered')).toHaveLength(2))
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Submission updated', 'success')
  })

  it('deletes a submission after confirmation and reloads the submissions', async () => {
    setupApi({ submissionsByGame: { 7: [submission(1, 'alice', '10')] } })
    mockDelete.mockResolvedValue({})
    renderPage()
    fireEvent.click((await screen.findAllByRole('button', { name: 'Manage Submissions' }))[0])
    await screen.findByText('alice')

    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    await confirmDialog()

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('/submissions/1'))
    await waitFor(() => expect(mockGet.mock.calls.filter(c => c[0] === '/submissions/game/7/unfiltered')).toHaveLength(2))
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Submission deleted', 'success')
  })

  it('ignores a slower submissions response for a previously selected game', async () => {
    let resolveWordle
    mockGet.mockImplementation((url) => {
      if (url === '/games') return Promise.resolve({ data: [wordle, tango] })
      if (url === '/submissions/game/7/unfiltered') return new Promise(resolve => { resolveWordle = resolve })
      if (url === '/submissions/game/8/unfiltered') return Promise.resolve({ data: [submission(2, 'bob', '3')] })
      return Promise.reject(new Error(`unexpected GET ${url}`))
    })
    renderPage()
    const manageButtons = await screen.findAllByRole('button', { name: 'Manage Submissions' })

    fireEvent.click(manageButtons[0])
    fireEvent.click(manageButtons[1])
    await screen.findByText('bob')
    resolveWordle({ data: [submission(1, 'alice', '10')] })

    await new Promise(r => setTimeout(r, 20))
    expect(screen.queryByText('alice')).not.toBeInTheDocument()
    expect(screen.getByText('bob')).toBeInTheDocument()
  })
})
