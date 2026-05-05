import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, act, waitFor } from '@testing-library/react'
import useAsyncData from '../hooks/useAsyncData'

describe('useAsyncData', () => {
  it('starts in loading state', () => {
    const fetcher = vi.fn(() => new Promise(() => {})) // never resolves
    const { result } = renderHook(() => useAsyncData(fetcher, []))
    expect(result.current.loading).toBe(true)
    expect(result.current.data).toBeNull()
    expect(result.current.error).toBe(false)
  })

  it('resolves data and clears loading', async () => {
    const fetcher = vi.fn().mockResolvedValue({ id: 1 })
    const { result } = renderHook(() => useAsyncData(fetcher, []))

    await waitFor(() => expect(result.current.loading).toBe(false))

    expect(result.current.data).toEqual({ id: 1 })
    expect(result.current.error).toBe(false)
  })

  it('sets error on rejection', async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error('fail'))
    const { result } = renderHook(() => useAsyncData(fetcher, []))

    await waitFor(() => expect(result.current.loading).toBe(false))

    expect(result.current.error).toBe(true)
    expect(result.current.data).toBeNull()
  })

  it('re-fetches when refetch() is called', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce('first')
      .mockResolvedValueOnce('second')

    const { result } = renderHook(() => useAsyncData(fetcher, []))

    await waitFor(() => expect(result.current.data).toBe('first'))

    act(() => result.current.refetch())

    await waitFor(() => expect(result.current.data).toBe('second'))
    expect(fetcher).toHaveBeenCalledTimes(2)
  })

  it('re-fetches when deps change', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce('a')
      .mockResolvedValueOnce('b')

    const { result, rerender } = renderHook(
      ({ id }) => useAsyncData(fetcher, [id]),
      { initialProps: { id: 1 } }
    )

    await waitFor(() => expect(result.current.data).toBe('a'))

    rerender({ id: 2 })

    await waitFor(() => expect(result.current.data).toBe('b'))
    expect(fetcher).toHaveBeenCalledTimes(2)
  })

  it('resets error state on subsequent fetch', async () => {
    const fetcher = vi.fn()
      .mockRejectedValueOnce(new Error('fail'))
      .mockResolvedValueOnce('ok')

    const { result } = renderHook(() => useAsyncData(fetcher, []))

    await waitFor(() => expect(result.current.error).toBe(true))

    act(() => result.current.refetch())

    await waitFor(() => expect(result.current.data).toBe('ok'))
    expect(result.current.error).toBe(false)
  })
})
