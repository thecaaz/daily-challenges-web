import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'

const mockGet = vi.fn()
vi.mock('../api', () => ({
  default: { get: (...a) => mockGet(...a) },
  getApiRoot: () => '',
}))

import useLeagueGameSummaries from '../hooks/useLeagueGameSummaries'
import { buildRecentPlaysData } from '../components/League/RecentPlaysBar'

describe('buildRecentPlaysData', () => {
  it("labels each bar with the API's scoring day, the newest as Today", () => {
    const data = buildRecentPlaysData([1, 0, 3], ['2026-11-01', '2026-11-02', '2026-11-03'])

    expect(data.map(d => d.isoDate)).toEqual(['2026-11-01', '2026-11-02', '2026-11-03'])
    expect(data.map(d => d.label)).toEqual(['Nov 1', 'Nov 2', 'Today'])
    expect(data.map(d => d.count)).toEqual([1, 0, 3])
  })
})

describe('useLeagueGameSummaries.loadMore', () => {
  beforeEach(() => { mockGet.mockReset() })

  async function renderFirstPage() {
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 1 }], totalCount: 3 } })
    const rendered = renderHook(() => useLeagueGameSummaries(5))
    await waitFor(() => expect(rendered.result.current.items).toHaveLength(1))
    return rendered
  }

  it('asks for the same page again after a failed load', async () => {
    const { result } = await renderFirstPage()
    mockGet.mockRejectedValueOnce(new Error('boom'))
    await act(async () => { result.current.loadMore() })
    await waitFor(() => expect(result.current.error).toBeTruthy())
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 2 }], totalCount: 3 } })

    await act(async () => { result.current.loadMore() })

    await waitFor(() => expect(result.current.items).toHaveLength(2))
    expect(mockGet.mock.calls.map(call => call[1].params.page)).toEqual([1, 2, 2])
    expect(result.current.error).toBeNull()
  })

  it('advances to the next page after a successful load', async () => {
    const { result } = await renderFirstPage()
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 2 }], totalCount: 3 } })
    await act(async () => { result.current.loadMore() })
    await waitFor(() => expect(result.current.items).toHaveLength(2))
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 3 }], totalCount: 3 } })

    await act(async () => { result.current.loadMore() })

    await waitFor(() => expect(result.current.items).toHaveLength(3))
    expect(mockGet.mock.calls.map(call => call[1].params.page)).toEqual([1, 2, 3])
  })
})

describe('useLeagueGameSummaries resets', () => {
  beforeEach(() => { mockGet.mockReset() })

  it('reloads the first page when the period changes', async () => {
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 1 }], totalCount: 1 } })
    const { result } = renderHook(() => useLeagueGameSummaries(5))
    await waitFor(() => expect(result.current.items).toHaveLength(1))

    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 2 }, { gameId: 3 }], totalCount: 2 } })
    act(() => { result.current.setDays(30) })

    await waitFor(() => expect(result.current.items.map(i => i.gameId)).toEqual([2, 3]))
    expect(result.current.days).toBe(30)
    expect(result.current.totalCount).toBe(2)
    expect(mockGet.mock.calls[1][1].params).toEqual({ days: 30, page: 1, pageSize: 12 })
  })

  it('ignores a slower response once a refresh has superseded it', async () => {
    let resolveFirst
    mockGet.mockReturnValueOnce(new Promise(resolve => { resolveFirst = resolve }))
    const { result } = renderHook(() => useLeagueGameSummaries(5))

    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 2 }], totalCount: 1 } })
    act(() => { result.current.refresh() })
    await waitFor(() => expect(result.current.items.map(i => i.gameId)).toEqual([2]))
    await act(async () => { resolveFirst({ data: { items: [{ gameId: 1 }], totalCount: 1 } }) })

    expect(result.current.items.map(i => i.gameId)).toEqual([2])
    expect(result.current.loading).toBe(false)
  })

  it('replaces the items on refresh instead of appending', async () => {
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 1 }], totalCount: 2 } })
    const { result } = renderHook(() => useLeagueGameSummaries(5))
    await waitFor(() => expect(result.current.items).toHaveLength(1))

    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 1 }], totalCount: 2 } })
    await act(async () => { result.current.refresh() })

    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2))
    expect(result.current.items).toHaveLength(1)
    expect(mockGet.mock.calls[1][1].params.page).toBe(1)
  })
})
