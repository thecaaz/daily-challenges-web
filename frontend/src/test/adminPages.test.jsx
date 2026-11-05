import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

let mockAuth = { user: null, loading: true }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

const mockShowSnackbar = vi.fn()
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => ({ showSnackbar: mockShowSnackbar }),
}))

const mockGet = vi.fn()
const mockPost = vi.fn()
const mockPut = vi.fn()
vi.mock('../api', () => ({
  default: {
    get: (...a) => mockGet(...a),
    post: (...a) => mockPost(...a),
    put: (...a) => mockPut(...a),
    delete: vi.fn(),
  },
  getApiRoot: () => '',
}))

import Admin from '../pages/Admin'
import AdminUsers, { parseXpAmount } from '../pages/AdminUsers'

const admin = { id: 1, username: 'root', isAdmin: true }

function renderAt(ui) {
  return render(<MemoryRouter>{ui}</MemoryRouter>)
}

describe('admin pages survive auth going from loading to admin', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAuth = { user: null, loading: true }
  })

  it('Admin renders after auth finishes loading', async () => {
    mockGet.mockResolvedValue({ data: [{ id: 7, name: 'Wordle', resetTime: '00:00' }] })

    const { rerender, container } = renderAt(<Admin />)
    expect(container).toBeEmptyDOMElement()
    expect(mockGet).not.toHaveBeenCalled()

    mockAuth = { user: admin, loading: false }
    rerender(<MemoryRouter><Admin /></MemoryRouter>)

    await waitFor(() => expect(screen.getByText('Wordle')).toBeInTheDocument())
    expect(mockGet).toHaveBeenCalledWith('/games')
  })

  it('AdminUsers renders and loads users after auth finishes loading', async () => {
    mockGet.mockResolvedValue({ data: { items: [{ id: 2, username: 'bob', totalXp: 10, level: 1, xpToNextLevel: 5, streak: 0 }], totalCount: 1 } })

    const { rerender, container } = renderAt(<AdminUsers />)
    expect(container).toBeEmptyDOMElement()
    expect(mockGet).not.toHaveBeenCalled()

    mockAuth = { user: admin, loading: false }
    rerender(<MemoryRouter><AdminUsers /></MemoryRouter>)

    await waitFor(() => expect(screen.getByText('bob')).toBeInTheDocument())
    expect(mockGet).toHaveBeenCalledWith('/admin/users', { params: { page: 1, pageSize: 50 } })
  })

  it('Admin shows local reset times and sends UTC, without the offset field', async () => {
    const tz = vi.spyOn(Date.prototype, 'getTimezoneOffset').mockReturnValue(-60) // UTC+1
    try {
      mockAuth = { user: admin, loading: false }
      mockGet.mockResolvedValue({ data: [{ id: 7, name: 'Wordle', resetTime: '23:30', url: 'https://x.test', description: 'd', rankingMode: 'highest' }] })
      mockPut.mockResolvedValue({ data: {} })
      mockPost.mockResolvedValue({ data: {} })
      const { container } = renderAt(<Admin />)

      expect(await screen.findByText(/Resets at 00:30 local \(23:30 UTC\)/)).toBeInTheDocument()

      fireEvent.click(screen.getByRole('button', { name: 'Edit Wordle' }))
      const timeInputs = container.querySelectorAll('input[type="time"]')
      expect(timeInputs[1]).toHaveValue('00:30')

      // Clear the URL and save: url is sent as "" and resetTime round-trips to UTC.
      fireEvent.change(screen.getByLabelText('URL'), { target: { value: '' } })
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
      await waitFor(() => expect(mockPut).toHaveBeenCalled())
      const fd = mockPut.mock.calls[0][1]
      expect(fd.get('resetTime')).toBe('23:30')
      expect(fd.get('url')).toBe('')
      expect(fd.get('description')).toBe('d')
      expect(fd.has('resetTimezoneOffsetMinutes')).toBe(false)

      // Create: local 06:00 -> 05:00 UTC.
      fireEvent.change(screen.getByLabelText(/Game name/), { target: { value: 'New' } })
      fireEvent.change(container.querySelectorAll('input[type="time"]')[0], { target: { value: '06:00' } })
      fireEvent.click(screen.getByRole('button', { name: 'Create Game' }))
      await waitFor(() => expect(mockPost).toHaveBeenCalled())
      const created = mockPost.mock.calls[0][1]
      expect(created.get('resetTime')).toBe('05:00')
      expect(created.get('url')).toBe('')
      expect(created.has('resetTimezoneOffsetMinutes')).toBe(false)
    } finally {
      tz.mockRestore()
    }
  })

  it('AdminUsers sends the search term and accepts only non-zero whole XP amounts', async () => {
    mockAuth = { user: admin, loading: false }
    mockGet.mockResolvedValue({ data: { items: [{ id: 2, username: 'bob', totalXp: 10, level: 1, xpToNextLevel: 5, streak: 0 }], totalCount: 1 } })
    renderAt(<AdminUsers />)
    await screen.findByText('bob')

    fireEvent.change(screen.getByLabelText('Search username'), { target: { value: ' bo ' } })
    await waitFor(() => expect(mockGet).toHaveBeenCalledWith('/admin/users', { params: { page: 1, pageSize: 50, search: 'bo' } }))

    expect(parseXpAmount('5')).toBe(5)
    expect(parseXpAmount('-5')).toBe(5)
    expect(parseXpAmount('0')).toBeNull()
    expect(parseXpAmount('1.5')).toBeNull()
    expect(parseXpAmount('1e3')).toBeNull()
    expect(parseXpAmount('')).toBeNull()
  })
})
