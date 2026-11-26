import React from 'react'
import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'

vi.mock('../components/DateCalendar', () => ({
  default: ({ onChange }) => <button type="button" onClick={() => onChange('2026-11-20')}>Pick 20th</button>,
}))

import DatePickerButton from '../components/DatePickerButton'

function renderPicker(selectedDate) {
  const onChange = vi.fn()
  render(<DatePickerButton availableDates={['2026-11-20']} selectedDate={selectedDate} onChange={onChange} />)
  return onChange
}

describe('DatePickerButton', () => {
  it('shows "All" when no day is selected and offers no reset', () => {
    renderPicker('')

    fireEvent.click(screen.getByRole('button', { name: 'All' }))

    expect(screen.getByText('Day')).toBeInTheDocument()
    expect(screen.getAllByText('All')).toHaveLength(1)
  })

  it('selects a day from the calendar and closes', async () => {
    const onChange = renderPicker('')
    fireEvent.click(screen.getByRole('button', { name: 'All' }))

    fireEvent.click(screen.getByRole('button', { name: 'Pick 20th' }))

    expect(onChange).toHaveBeenCalledWith('2026-11-20')
    await waitFor(() => expect(screen.queryByText('Day')).not.toBeInTheDocument())
  })

  it('resets to all days and closes', async () => {
    const onChange = renderPicker('2026-11-20')
    fireEvent.click(screen.getByRole('button', { name: '2026-11-20' }))

    fireEvent.click(screen.getByRole('button', { name: 'All' }))

    expect(onChange).toHaveBeenCalledWith('')
    await waitFor(() => expect(screen.queryByText('Day')).not.toBeInTheDocument())
  })
})
