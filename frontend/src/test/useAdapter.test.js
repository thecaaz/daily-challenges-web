import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import useAdapter from '../hooks/useAdapter'
import * as adapters from '../utils/adapters'

vi.mock('../utils/adapters', () => ({
  getAdapterForUrl: vi.fn(),
}))

describe('useAdapter', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('returns null when url is empty', async () => {
    const { result } = renderHook(() => useAdapter(null))
    expect(result.current).toBeNull()
  })

  it('returns null when url is undefined', async () => {
    const { result } = renderHook(() => useAdapter(undefined))
    expect(result.current).toBeNull()
  })

  it('returns adapter when one is found for the url', async () => {
    const fakeAdapter = { name: 'TestAdapter', matchDescriptor: { type: 'equals', value: 'example.com' } }
    adapters.getAdapterForUrl.mockResolvedValue(fakeAdapter)

    const { result } = renderHook(() => useAdapter('https://example.com'))

    await waitFor(() => expect(result.current).toEqual(fakeAdapter))
    expect(adapters.getAdapterForUrl).toHaveBeenCalledWith('https://example.com')
  })

  it('returns null when no adapter matches', async () => {
    adapters.getAdapterForUrl.mockResolvedValue(null)

    const { result } = renderHook(() => useAdapter('https://unknown.com'))

    await waitFor(() => expect(adapters.getAdapterForUrl).toHaveBeenCalled())
    expect(result.current).toBeNull()
  })

  it('returns null when getAdapterForUrl rejects', async () => {
    adapters.getAdapterForUrl.mockRejectedValue(new Error('network error'))

    const { result } = renderHook(() => useAdapter('https://error.com'))

    await waitFor(() => expect(adapters.getAdapterForUrl).toHaveBeenCalled())
    expect(result.current).toBeNull()
  })

  it('re-fetches when url changes', async () => {
    const adapterA = { name: 'AdapterA' }
    const adapterB = { name: 'AdapterB' }
    adapters.getAdapterForUrl
      .mockResolvedValueOnce(adapterA)
      .mockResolvedValueOnce(adapterB)

    const { result, rerender } = renderHook(({ url }) => useAdapter(url), {
      initialProps: { url: 'https://a.com' },
    })

    await waitFor(() => expect(result.current).toEqual(adapterA))

    rerender({ url: 'https://b.com' })

    await waitFor(() => expect(result.current).toEqual(adapterB))
  })
})
