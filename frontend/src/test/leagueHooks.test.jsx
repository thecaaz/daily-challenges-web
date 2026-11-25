import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const mockSnackbarValue = { showSnackbar: vi.fn() }
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => mockSnackbarValue,
}))

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => ({
  ...(await importOriginal()),
  useNavigate: () => mockNavigate,
}))

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...a) => mockGet(...a) },
  getApiRoot: () => '',
}))

import useLatestRequest from '../hooks/useLatestRequest'
import useLeague from '../hooks/useLeague'
import useLeagueLeaderboard from '../hooks/useLeagueLeaderboard'

const wrapper = ({ children }) => <MemoryRouter>{children}</MemoryRouter>

function deferred() {
  let resolve
  const promise = new Promise(r => { resolve = r })
  return { promise, resolve }
}

beforeEach(() => {
  mockGet.mockReset()
  mockNavigate.mockReset()
  mockSnackbarValue.showSnackbar.mockReset()
})

describe('useLatestRequest', () => {
  it('reports only the most recently started request as latest', () => {
    const { result } = renderHook(() => useLatestRequest())

    const isFirstLatest = result.current()
    const isSecondLatest = result.current()

    expect(isFirstLatest()).toBe(false)
    expect(isSecondLatest()).toBe(true)
  })
})

describe('useLeague', () => {
  it('keeps the league and only shows a snackbar when a refresh fails', async () => {
    mockGet.mockResolvedValueOnce({ data: { id: 7, name: 'Pals' } })
    const { result } = renderHook(() => useLeague(7), { wrapper })
    await waitFor(() => expect(result.current.loading).toBe(false))
    mockGet.mockRejectedValueOnce(new Error('boom'))

    await act(() => result.current.refresh())

    expect(result.current.league).toEqual({ id: 7, name: 'Pals' })
    expect(mockSnackbarValue.showSnackbar).toHaveBeenCalledWith('Failed to refresh league.', 'error')
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('ignores a slower initial response once a refresh has superseded it', async () => {
    const initial = deferred()
    mockGet.mockReturnValueOnce(initial.promise)
    mockGet.mockResolvedValueOnce({ data: { id: 7, name: 'Fresh' } })
    const { result } = renderHook(() => useLeague(7), { wrapper })

    await act(() => result.current.refresh())
    await act(async () => initial.resolve({ data: { id: 7, name: 'Stale' } }))

    expect(result.current.league.name).toBe('Fresh')
  })

  it('updates only the name when the league is renamed', async () => {
    mockGet.mockResolvedValueOnce({ data: { id: 7, name: 'Pals', ownerId: 1 } })
    const { result } = renderHook(() => useLeague(7), { wrapper })
    await waitFor(() => expect(result.current.league).not.toBeNull())

    act(() => result.current.setLeagueName('Buddies'))

    expect(result.current.league).toEqual({ id: 7, name: 'Buddies', ownerId: 1 })
  })
})

describe('useLeagueLeaderboard', () => {
  function setupGames() {
    mockGet.mockImplementation((url) => {
      if (url === '/games') return Promise.resolve({ data: [{ id: 3, currentScoringDay: '2026-11-03' }] })
      return Promise.resolve({ data: { scoringDay: '2026-11-02', entries: [] } })
    })
  }

  function leaderboardCalls() {
    return mockGet.mock.calls.filter(c => c[0] === '/leagues/7/leaderboard')
  }

  it('does not load the leaderboard while hidden', async () => {
    setupGames()
    const { result } = renderHook(() => useLeagueLeaderboard(7, false))
    await waitFor(() => expect(result.current.games).toHaveLength(1))

    act(() => result.current.selectGame(3))

    expect(leaderboardCalls()).toHaveLength(0)
  })

  it("defaults a selected game to its current scoring day", async () => {
    setupGames()
    const { result } = renderHook(() => useLeagueLeaderboard(7, true))
    await waitFor(() => expect(result.current.games).toHaveLength(1))

    act(() => result.current.selectGame(3))

    await waitFor(() => expect(leaderboardCalls()).toHaveLength(1))
    expect(leaderboardCalls()[0][1].params).toEqual({ gameId: 3, scoringDay: '2026-11-03' })
    expect(result.current.displayedDay).toBe('2026-11-03')
  })

  it('keeps an explicitly chosen day when switching games', async () => {
    setupGames()
    const { result } = renderHook(() => useLeagueLeaderboard(7, true))
    await waitFor(() => expect(result.current.games).toHaveLength(1))
    act(() => result.current.selectGame(3, '2026-10-01'))

    act(() => result.current.changeGame(4))

    await waitFor(() => expect(leaderboardCalls().at(-1)[1].params).toEqual({ gameId: 4, scoringDay: '2026-10-01' }))
  })
})
