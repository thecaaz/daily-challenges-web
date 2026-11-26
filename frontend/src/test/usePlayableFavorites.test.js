import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, act, waitFor } from '@testing-library/react'
import usePlayableFavorites from '../hooks/usePlayableFavorites'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...args) => mockGet(...args) },
}))
const mockHasAdapterForUrl = vi.fn()
vi.mock('../utils/adapters', () => ({
  hasAdapterForUrl: (...args) => mockHasAdapterForUrl(...args),
}))

const aFavorite = (id, overrides) => ({ id, isFavorite: true, hasSubmittedForLatest: false, url: `https://g${id}.example/`, ...overrides })

const favoriteChanged = detail => act(() => {
  window.dispatchEvent(new CustomEvent('favorite-changed', { detail }))
})

async function renderLoaded(options) {
  const rendered = renderHook(() => usePlayableFavorites(1, options))
  await waitFor(() => expect(rendered.result.current.loading).toBe(false))
  return rendered
}

describe('usePlayableFavorites', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockHasAdapterForUrl.mockResolvedValue(false)
    mockGet.mockResolvedValue({ data: [{ id: 3, isFavorite: true, hasSubmittedForLatest: false }] })
  })

  it('splits the favourites into done and pending', async () => {
    const { result } = await renderLoaded()

    expect(result.current.total).toBe(1)
    expect(result.current.pendingGames).toHaveLength(1)
  })

  it('reloads when a favourite change is confirmed or rolled back', async () => {
    await renderLoaded()

    favoriteChanged({ gameId: 3, isFavorite: true, confirmed: true })
    favoriteChanged({ gameId: 3, isFavorite: false })

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(3))
  })

  it('ignores optimistic favourite changes', async () => {
    await renderLoaded()

    favoriteChanged({ gameId: 3, isFavorite: true, optimistic: true })

    expect(mockGet).toHaveBeenCalledTimes(1)
  })

  it('ignores favourite changes without subscribe', async () => {
    await renderLoaded({ subscribe: false })

    favoriteChanged({ gameId: 3, isFavorite: true, confirmed: true })

    expect(mockGet).toHaveBeenCalledTimes(1)
  })

  it('counts only favourites and how many are submitted', async () => {
    mockGet.mockResolvedValue({ data: [aFavorite(1), aFavorite(2, { hasSubmittedForLatest: true }), aFavorite(3, { isFavorite: false })] })

    const { result } = await renderLoaded()

    expect(result.current.total).toBe(2)
    expect(result.current.submittedCount).toBe(1)
    expect(result.current.pendingGames.map(game => game.id)).toEqual([1])
    expect(result.current.error).toBe(false)
  })

  it('treats a non-list response as no favourites', async () => {
    mockGet.mockResolvedValue({ data: { games: [] } })

    const { result } = await renderLoaded()

    expect(result.current.total).toBe(0)
  })

  it('lists the pending favourites the extension can capture, by id', async () => {
    mockGet.mockResolvedValue({ data: [aFavorite(9), aFavorite(2), aFavorite(5), aFavorite(7, { url: null })] })
    mockHasAdapterForUrl.mockImplementation(url => (url.includes('g5') ? Promise.reject(new Error('x')) : Promise.resolve(true)))

    const { result } = await renderLoaded()

    await waitFor(() => expect(result.current.playableCount).toBe(2))
    expect(result.current.playable.map(game => game.id)).toEqual([2, 9])
    expect(result.current.checkingPlayable).toBe(false)
    expect(mockHasAdapterForUrl).toHaveBeenCalledTimes(3)
  })

  it('reports an error and clears the favourites when /games fails', async () => {
    const { result } = await renderLoaded()
    mockGet.mockRejectedValue(new Error('down'))

    await act(() => result.current.reload())

    expect(result.current.error).toBe(true)
    expect(result.current.total).toBe(0)
    expect(result.current.loading).toBe(false)
  })

  it('is empty without a user, without asking the server', async () => {
    const { result } = renderHook(() => usePlayableFavorites(null))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current.total).toBe(0)
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('stays loading while disabled', () => {
    const { result } = renderHook(() => usePlayableFavorites(1, { enabled: false }))

    expect(result.current.loading).toBe(true)
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('drops a slower response for a previous user', async () => {
    let answerFirst
    mockGet
      .mockReturnValueOnce(new Promise(resolve => { answerFirst = resolve }))
      .mockResolvedValueOnce({ data: [aFavorite(4)] })
    const { result, rerender } = renderHook(({ userId }) => usePlayableFavorites(userId), { initialProps: { userId: 1 } })

    rerender({ userId: 2 })
    await waitFor(() => expect(result.current.total).toBe(1))
    await act(async () => answerFirst({ data: [aFavorite(1), aFavorite(2)] }))

    expect(result.current.pendingGames.map(game => game.id)).toEqual([4])
  })
})
