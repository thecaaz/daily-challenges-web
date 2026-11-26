import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...args) => mockGet(...args) },
  getApiRoot: () => '',
}))

let mockUser = { id: 5 }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: mockUser }),
}))

import useGame from '../hooks/useGame'
import useFriendStatus from '../hooks/useFriendStatus'
import useUserProfile from '../hooks/useUserProfile'

function deferred() {
  let resolve
  let reject
  const promise = new Promise((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

const httpError = status => Object.assign(new Error(`HTTP ${status}`), { response: { status } })

beforeEach(() => {
  mockGet.mockReset()
  mockUser = { id: 5 }
})

describe('useGame', () => {
  const overview = { game: { id: 3, name: 'Wordle' }, availableDates: ['2026-11-25'], hasSubmittedForLatest: 1 }

  it('loads the game overview', async () => {
    mockGet.mockResolvedValue({ data: overview })

    const { result } = renderHook(() => useGame(3))

    expect(result.current.loading).toBe(true)
    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(mockGet).toHaveBeenCalledWith('/games/3/overview?include=availableDates,hasSubmitted')
    expect(result.current).toMatchObject({
      game: overview.game, availableDates: overview.availableDates, hasSubmittedForLatest: true, notFound: false, error: null,
    })
  })

  it('does not fetch without a game id', async () => {
    const { result } = renderHook(() => useGame(undefined))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(mockGet).not.toHaveBeenCalled()
    expect(result.current.game).toBeNull()
  })

  it('treats an empty overview as not found', async () => {
    mockGet.mockResolvedValue({ data: {} })

    const { result } = renderHook(() => useGame(3))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current).toMatchObject({ notFound: true, game: null, availableDates: [], hasSubmittedForLatest: false })
  })

  it('clears the game on a 404', async () => {
    mockGet.mockResolvedValueOnce({ data: overview })
    const { result } = renderHook(() => useGame(3))
    await waitFor(() => expect(result.current.game).toEqual(overview.game))
    mockGet.mockRejectedValueOnce(httpError(404))

    await act(() => result.current.refresh())

    expect(result.current).toMatchObject({ notFound: true, game: null, availableDates: [], hasSubmittedForLatest: false, error: null, loading: false })
  })

  it('keeps the game and sets error on another failure', async () => {
    mockGet.mockResolvedValueOnce({ data: overview })
    const { result } = renderHook(() => useGame(3))
    await waitFor(() => expect(result.current.game).toEqual(overview.game))
    const failure = httpError(500)
    mockGet.mockRejectedValueOnce(failure)

    await act(() => result.current.refresh())

    expect(result.current).toMatchObject({ error: failure, notFound: false, game: overview.game, loading: false })
  })

  it('ignores the response of a superseded request', async () => {
    const slow = deferred()
    mockGet.mockReturnValueOnce(slow.promise)
    const { result } = renderHook(() => useGame(3))
    mockGet.mockResolvedValueOnce({ data: { ...overview, game: { id: 3, name: 'Newer' } } })
    await act(() => result.current.refresh())

    await act(async () => { slow.resolve({ data: overview }) })

    expect(result.current.game).toEqual({ id: 3, name: 'Newer' })
    expect(result.current.loading).toBe(false)
  })

  it('refetches when the viewer changes', async () => {
    mockGet.mockResolvedValue({ data: overview })
    const { result, rerender } = renderHook(() => useGame(3))
    await waitFor(() => expect(result.current.loading).toBe(false))

    mockUser = { id: 6 }
    rerender()

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
  })
})

describe('useFriendStatus', () => {
  it('loads the friendship status', async () => {
    mockGet.mockResolvedValue({ data: { status: 'friends' } })

    const { result } = renderHook(() => useFriendStatus(9))

    await waitFor(() => expect(result.current.status).toBe('friends'))
    expect(mockGet).toHaveBeenCalledWith('/friends/status/9')
    expect(result.current).toMatchObject({ loading: false, error: false })
  })

  it('is "none" without a target or when logged out', async () => {
    mockUser = null
    const { result } = renderHook(() => useFriendStatus(9))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current).toMatchObject({ status: 'none', error: false })
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('keeps the previous status and sets error on failure', async () => {
    mockGet.mockResolvedValueOnce({ data: { status: 'pending_sent' } })
    const { result } = renderHook(() => useFriendStatus(9))
    await waitFor(() => expect(result.current.status).toBe('pending_sent'))
    mockGet.mockRejectedValueOnce(new Error('down'))

    act(() => result.current.refresh())

    await waitFor(() => expect(result.current.error).toBe(true))
    expect(result.current).toMatchObject({ status: 'pending_sent', loading: false })
  })

  it('ignores the response of a superseded request', async () => {
    const slow = deferred()
    mockGet.mockReturnValueOnce(slow.promise)
    const { result } = renderHook(() => useFriendStatus(9))
    mockGet.mockResolvedValueOnce({ data: { status: 'friends' } })
    act(() => result.current.refresh())
    await waitFor(() => expect(result.current.status).toBe('friends'))

    await act(async () => { slow.resolve({ data: { status: 'none' } }) })

    expect(result.current).toMatchObject({ status: 'friends', loading: false })
  })
})

describe('useUserProfile', () => {
  it('loads the profile with the top games count', async () => {
    mockGet.mockResolvedValue({ data: { userName: 'ann' } })

    const { result } = renderHook(() => useUserProfile(7, 5))

    expect(result.current.loading).toBe(true)
    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(mockGet).toHaveBeenCalledWith('/users/7/profile', { params: { topGames: 5 } })
    expect(result.current).toMatchObject({ data: { userName: 'ann' }, error: null })
  })

  it('is not loading and has no data without a user id', () => {
    const { result } = renderHook(() => useUserProfile(null))

    expect(result.current).toEqual({ data: null, loading: false, error: null })
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('exposes the axios error', async () => {
    const failure = httpError(404)
    mockGet.mockRejectedValue(failure)

    const { result } = renderHook(() => useUserProfile(7))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current).toMatchObject({ data: null, error: failure })
  })

  it('ignores the response for a previous user', async () => {
    const slow = deferred()
    mockGet.mockReturnValueOnce(slow.promise).mockResolvedValueOnce({ data: { userName: 'bob' } })
    const { result, rerender } = renderHook(({ userId }) => useUserProfile(userId), { initialProps: { userId: 7 } })

    rerender({ userId: 8 })
    await waitFor(() => expect(result.current.data).toEqual({ userName: 'bob' }))
    await act(async () => { slow.resolve({ data: { userName: 'ann' } }) })

    expect(result.current).toMatchObject({ data: { userName: 'bob' }, loading: false, error: null })
  })

  it('refetches when the viewer changes', async () => {
    mockGet.mockResolvedValue({ data: { userName: 'ann' } })
    const { result, rerender } = renderHook(() => useUserProfile(7))
    await waitFor(() => expect(result.current.loading).toBe(false))

    mockUser = { id: 6 }
    rerender()

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
  })
})
