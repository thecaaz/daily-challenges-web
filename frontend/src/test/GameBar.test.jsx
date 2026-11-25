import React from 'react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const mockLogout = vi.fn()
let mockAuthValue
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuthValue,
}))
const mockToggleMode = vi.fn()
let mockMode = 'light'
vi.mock('../contexts/ThemeContext', () => ({
  useThemeMode: () => ({ mode: mockMode, toggleMode: mockToggleMode }),
}))
vi.mock('../components/NotificationBell', () => ({ default: () => <div data-testid="bell" /> }))
vi.mock('../components/VersionBadge', () => ({ default: () => <div data-testid="version" /> }))

import GameBar from '../components/GameBar'

const player = { id: 4, username: 'ann', level: 3, xpIntoLevel: 40, xpToNextLevel: 60, streak: 5, totalXp: 340 }

function renderBar(path = '/somewhere') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/" element={<><GameBar /><div>Home page</div></>} />
        <Route path="*" element={<GameBar />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('GameBar', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockMode = 'light'
    mockAuthValue = { user: player, logout: mockLogout }
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows Login and Register when logged out', () => {
    mockAuthValue = { user: null, logout: mockLogout }
    renderBar()

    expect(screen.getByRole('link', { name: 'Login' })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'Register' })).toHaveAttribute('href', '/register')
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
    expect(screen.queryByTestId('bell')).not.toBeInTheDocument()
  })

  it('shows level, XP progress, streak and profile link when logged in', () => {
    renderBar()

    const progress = screen.getByRole('progressbar', { name: 'XP progress: 40 of 100' })
    expect(progress).toHaveAttribute('aria-valuenow', '40')
    expect(screen.getByText('40/100 XP', { exact: false })).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('ann').closest('a')).toHaveAttribute('href', '/users/4')
    expect(screen.getByTestId('bell')).toBeInTheDocument()
  })

  it('uses defaults for a user without XP data and hides a streak of 1', () => {
    mockAuthValue = { user: { id: 4, username: 'ann', streak: 1 }, logout: mockLogout }
    renderBar()

    expect(screen.getByRole('progressbar', { name: 'XP progress: 0 of 100' })).toHaveAttribute('aria-valuenow', '0')
    expect(screen.queryByText('1', { selector: '.MuiChip-label' })).not.toBeInTheDocument()
  })

  it('toggles the colour scheme', () => {
    mockMode = 'dark'
    renderBar()

    fireEvent.click(screen.getByRole('button', { name: 'Toggle color scheme' }))

    expect(mockToggleMode).toHaveBeenCalled()
    expect(screen.getByTestId('LightModeIcon')).toBeInTheDocument()
  })

  it('logs out and goes home even when logout fails', async () => {
    mockLogout.mockRejectedValue(new Error('offline'))
    renderBar()

    fireEvent.click(screen.getByRole('button', { name: 'Logout' }))

    expect(await screen.findByText('Home page')).toBeInTheDocument()
    expect(mockLogout).toHaveBeenCalled()
  })

  it('opens the navigation drawer', async () => {
    renderBar()

    fireEvent.click(screen.getByRole('button', { name: 'open navigation' }))

    expect(await screen.findByText('Friends')).toBeInTheDocument()
  })

  it('flashes the XP gained when total XP increases, then hides it', () => {
    vi.useFakeTimers()
    const { rerender } = renderBar()
    expect(screen.queryByText(/\+.*XP$/)).not.toBeInTheDocument()

    mockAuthValue = { user: { ...player, totalXp: 365, xpIntoLevel: 65, xpToNextLevel: 35 }, logout: mockLogout }
    rerender(
      <MemoryRouter initialEntries={['/somewhere']}>
        <Routes><Route path="*" element={<GameBar />} /></Routes>
      </MemoryRouter>
    )

    expect(screen.getByText('+25 XP')).toBeInTheDocument()
    act(() => { vi.advanceTimersByTime(2500) })
    expect(screen.queryByText('+25 XP')).not.toBeInTheDocument()
  })

  it('does not flash when total XP decreases', async () => {
    const { rerender } = renderBar()

    mockAuthValue = { user: { ...player, totalXp: 300 }, logout: mockLogout }
    rerender(
      <MemoryRouter initialEntries={['/somewhere']}>
        <Routes><Route path="*" element={<GameBar />} /></Routes>
      </MemoryRouter>
    )

    await waitFor(() => expect(screen.queryByText(/^\+.* XP$/)).not.toBeInTheDocument())
  })
})
