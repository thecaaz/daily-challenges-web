import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const mockShowSnackbar = vi.fn()
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => ({ showSnackbar: mockShowSnackbar }),
}))

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...a) => mockGet(...a) },
  getApiRoot: () => '',
}))

import AdminUserAuditModal, { nextDay, buildFilterParams } from '../components/AdminUserAuditModal'

function renderModal() {
  return render(
    <MemoryRouter>
      <AdminUserAuditModal open onClose={() => {}} userId={5} username="bob" />
    </MemoryRouter>
  )
}

const lastParams = () => mockGet.mock.calls[mockGet.mock.calls.length - 1][1].params

describe('AdminUserAuditModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockResolvedValue({ data: { items: [{ id: 1, amount: 10, eventType: 'day_win', createdAt: '2026-01-02T10:00:00Z', scoringDay: '2026-01-01' }], totalCount: 1 } })
  })

  it('nextDay handles month and year ends', () => {
    expect(nextDay('2026-01-31')).toBe('2026-02-01')
    expect(nextDay('2026-12-31')).toBe('2027-01-01')
    expect(nextDay('2028-02-28')).toBe('2028-02-29')
    expect(nextDay('garbage')).toBeNull()
  })

  it('buildFilterParams makes "to" inclusive by sending the next day', () => {
    expect(buildFilterParams({ from: '2026-03-01', to: '2026-03-05', eventType: 'day_win' }))
      .toEqual({ from: '2026-03-01', to: '2026-03-06', eventType: 'day_win' })
    expect(buildFilterParams({ from: '', to: '', eventType: '' })).toEqual({})
  })

  it('loads on open, sends to+1 day on Apply, and Reset clears filters', async () => {
    renderModal()
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(1))
    expect(mockGet).toHaveBeenCalledWith('/admin/users/5/xp-events', { params: { page: 1, pageSize: 20 } })
    expect(await screen.findByText('2026-01-01')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-03-01' } })
    fireEvent.change(screen.getByLabelText('To (inclusive)'), { target: { value: '2026-03-31' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
    expect(lastParams()).toEqual({ page: 1, pageSize: 20, from: '2026-03-01', to: '2026-04-01' })

    fireEvent.click(screen.getByRole('button', { name: 'Reset' }))
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(3))
    expect(lastParams()).toEqual({ page: 1, pageSize: 20 })
    expect(screen.getByLabelText('From')).toHaveValue('')
  })

  it('offers day_win and submission_revoked, not streak_bonus', async () => {
    renderModal()
    await waitFor(() => expect(mockGet).toHaveBeenCalled())
    const select = document.querySelector('[aria-haspopup="listbox"]')
    fireEvent.mouseDown(select)
    expect(await screen.findByRole('option', { name: 'Day Win' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Submission Revoked' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Streak/i })).toBeNull()
  })
})
