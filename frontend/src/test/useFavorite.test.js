import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, act, waitFor } from '@testing-library/react'
import useFavorite from '../hooks/useFavorite'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockNavigate = vi.fn()
vi.mock('react-router-dom', () => ({ useNavigate: () => mockNavigate }))

const mockShowSnackbar = vi.fn()
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => ({ showSnackbar: mockShowSnackbar }),
}))

const mockApiPost = vi.fn()
const mockApiDelete = vi.fn()
vi.mock('../api', () => ({
  default: { post: (...args) => mockApiPost(...args), delete: (...args) => mockApiDelete(...args) },
}))

let mockUser = { id: 1, username: 'testuser' }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: mockUser }),
}))

describe('useFavorite', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUser = { id: 1, username: 'testuser' }
  })

  it('initialises with the provided initial value', () => {
    const { result } = renderHook(() => useFavorite(42, true))
    expect(result.current.isFavorite).toBe(true)
    expect(result.current.loading).toBe(false)
  })

  it('initialises to false when initial is not given', () => {
    const { result } = renderHook(() => useFavorite(42))
    expect(result.current.isFavorite).toBe(false)
  })

  it('toggles to true and calls POST', async () => {
    mockApiPost.mockResolvedValue({})
    const { result } = renderHook(() => useFavorite(42, false))

    act(() => { result.current.toggle() })

    // Optimistic update fires immediately
    expect(result.current.isFavorite).toBe(true)

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(mockApiPost).toHaveBeenCalledWith('/favorites/42')
    expect(result.current.isFavorite).toBe(true)
  })

  it('toggles to false and calls DELETE', async () => {
    mockApiDelete.mockResolvedValue({})
    const { result } = renderHook(() => useFavorite(42, true))

    act(() => { result.current.toggle() })

    expect(result.current.isFavorite).toBe(false)

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(mockApiDelete).toHaveBeenCalledWith('/favorites/42')
  })

  it('reverts optimistic update on API failure', async () => {
    mockApiPost.mockRejectedValue(new Error('network'))
    const { result } = renderHook(() => useFavorite(42, false))

    act(() => { result.current.toggle() })
    expect(result.current.isFavorite).toBe(true) // optimistic

    await waitFor(() => expect(result.current.loading).toBe(false))

    expect(result.current.isFavorite).toBe(false) // reverted
    expect(mockShowSnackbar).toHaveBeenCalledWith('Failed to update favorite', 'error')
  })

  it('redirects to login when user is not authenticated', async () => {
    mockUser = null
    const { result } = renderHook(() => useFavorite(42, false))

    await act(async () => { result.current.toggle() })

    expect(mockNavigate).toHaveBeenCalledWith('/login')
    expect(mockApiPost).not.toHaveBeenCalled()
  })

  it('syncs isFavorite when initial prop changes', () => {
    const { result, rerender } = renderHook(
      ({ initial }) => useFavorite(42, initial),
      { initialProps: { initial: false } }
    )
    expect(result.current.isFavorite).toBe(false)

    rerender({ initial: true })
    expect(result.current.isFavorite).toBe(true)
  })

  it('dispatches favorite-changed custom event on toggle', async () => {
    mockApiPost.mockResolvedValue({})
    const listener = vi.fn()
    window.addEventListener('favorite-changed', listener)

    const { result } = renderHook(() => useFavorite(42, false))
    act(() => { result.current.toggle() })

    await waitFor(() => expect(result.current.loading).toBe(false))

    expect(listener).toHaveBeenCalled()
    window.removeEventListener('favorite-changed', listener)
  })
})
