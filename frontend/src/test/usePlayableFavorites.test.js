import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, act, waitFor } from '@testing-library/react'
import usePlayableFavorites from '../hooks/usePlayableFavorites'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...args) => mockGet(...args) },
}))
vi.mock('../utils/adapters', () => ({
  hasAdapterForUrl: () => Promise.resolve(false),
}))

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
})
