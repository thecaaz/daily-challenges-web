import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const mockDownloadBlob = vi.fn()
vi.mock('../utils/downloadBlob', () => ({
  default: (...a) => mockDownloadBlob(...a),
}))

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

  it.each([
    ['2026-01-31', '2026-02-01'],
    ['2026-12-31', '2027-01-01'],
    ['2028-02-28', '2028-02-29'],
  ])('nextDay(%s) is %s across month, year and leap-day ends', (day, expected) => {
    expect(nextDay(day)).toBe(expected)
  })

  it('nextDay rejects a value that is not a day', () => {
    expect(nextDay('garbage')).toBeNull()
  })

  it('buildFilterParams makes "to" inclusive by sending the next day', () => {
    const params = buildFilterParams({ from: '2026-03-01', to: '2026-03-05', eventType: 'day_win' })

    expect(params).toEqual({ from: '2026-03-01', to: '2026-03-06', eventType: 'day_win' })
  })

  it('buildFilterParams leaves out empty filters', () => {
    expect(buildFilterParams({ from: '', to: '', eventType: '' })).toEqual({})
  })

  it('loads the first page when opened', async () => {
    renderModal()

    expect(await screen.findByText('2026-01-01')).toBeInTheDocument()
    expect(mockGet).toHaveBeenCalledTimes(1)
    expect(mockGet).toHaveBeenCalledWith('/admin/users/5/xp-events', { params: { page: 1, pageSize: 20 } })
  })

  it('applies the filters, sending the day after "to"', async () => {
    renderModal()
    await screen.findByText('2026-01-01')
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-03-01' } })
    fireEvent.change(screen.getByLabelText('To (inclusive)'), { target: { value: '2026-03-31' } })

    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
    expect(lastParams()).toEqual({ page: 1, pageSize: 20, from: '2026-03-01', to: '2026-04-01' })
  })

  it('clears the filters and reloads on Reset', async () => {
    renderModal()
    await screen.findByText('2026-01-01')
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-03-01' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))

    fireEvent.click(screen.getByRole('button', { name: 'Reset' }))

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(3))
    expect(lastParams()).toEqual({ page: 1, pageSize: 20 })
    expect(screen.getByLabelText('From')).toHaveValue('')
  })

  it('offers day_win and submission_revoked, not streak_bonus', async () => {
    renderModal()
    await waitFor(() => expect(mockGet).toHaveBeenCalled())

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Event Type' }))

    expect(await screen.findByRole('option', { name: 'Day Win' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Submission Revoked' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Streak/i })).toBeNull()
  })

  it('pages with the applied filters, not the ones still being edited', async () => {
    mockGet.mockResolvedValue({ data: { items: [{ id: 1, amount: 10, eventType: 'day_win', createdAt: '2026-01-02T10:00:00Z' }], totalCount: 45 } })
    renderModal()
    await screen.findByText('Showing 1 of 45')
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-03-01' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
    expect(screen.getByRole('button', { name: 'Prev' })).toBeDisabled()

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-05-01' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(3))
    expect(lastParams()).toEqual({ page: 2, pageSize: 20, from: '2026-03-01' })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Prev' })).not.toBeDisabled())
  })

  it('disables Next on the last page', async () => {
    renderModal()

    await screen.findByText('Showing 1 of 1')
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('exports the applied filters as CSV', async () => {
    renderModal()
    await screen.findByText('2026-01-01')
    fireEvent.change(screen.getByLabelText('To (inclusive)'), { target: { value: '2026-03-31' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
    mockGet.mockResolvedValueOnce({ data: 'csv' })

    fireEvent.click(screen.getByRole('button', { name: 'Export CSV' }))

    await waitFor(() => expect(mockDownloadBlob).toHaveBeenCalled())
    expect(mockGet).toHaveBeenLastCalledWith('/admin/users/5/xp-events/export', { params: { to: '2026-04-01' }, responseType: 'blob' })
    expect(mockDownloadBlob.mock.calls[0][0]).toBe('xp-events-user-5.csv')
  })

  it('shows a snackbar when loading fails', async () => {
    mockGet.mockRejectedValue(new Error('boom'))
    renderModal()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Failed to load XP events', 'error'))
  })

  it('titles the dialog with the user id when the username is unknown', async () => {
    render(<MemoryRouter><AdminUserAuditModal open onClose={() => {}} userId={5} username={null} /></MemoryRouter>)

    expect(await screen.findByText(/User 5/)).toBeInTheDocument()
  })
})
