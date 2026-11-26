import React from 'react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, act } from '@testing-library/react'
import { ThemeProvider, useThemeMode } from '../contexts/ThemeContext'

function ModeProbe() {
  const { mode, toggleMode } = useThemeMode()
  return <button onClick={toggleMode}>{mode}</button>
}

const renderProvider = () => render(<ThemeProvider><ModeProbe /></ThemeProvider>)

describe('ThemeProvider', () => {
  let matchMedia

  beforeEach(() => {
    localStorage.clear()
    matchMedia = window.matchMedia
    window.matchMedia = vi.fn(() => ({ matches: false }))
  })

  afterEach(() => {
    window.matchMedia = matchMedia
    vi.restoreAllMocks()
  })

  it('uses the stored mode', () => {
    localStorage.setItem('themeMode', 'dark')

    renderProvider()

    expect(screen.getByRole('button')).toHaveTextContent('dark')
  })

  it('follows the system preference without a stored mode', () => {
    window.matchMedia = vi.fn(() => ({ matches: true }))

    renderProvider()

    expect(window.matchMedia).toHaveBeenCalledWith('(prefers-color-scheme: dark)')
    expect(screen.getByRole('button')).toHaveTextContent('dark')
  })

  it('ignores an unknown stored value and defaults to light', () => {
    localStorage.setItem('themeMode', 'sepia')

    renderProvider()

    expect(screen.getByRole('button')).toHaveTextContent('light')
  })

  it('toggles, persists and marks the document with the mode', () => {
    renderProvider()

    act(() => screen.getByRole('button').click())

    expect(screen.getByRole('button')).toHaveTextContent('dark')
    expect(localStorage.getItem('themeMode')).toBe('dark')
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')
  })

  it('falls back to light when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('denied') })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('denied') })

    renderProvider()

    expect(screen.getByRole('button')).toHaveTextContent('light')
  })
})
