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
  it('uses recentPlayDays from the API when present', () => {
    const data = buildRecentPlaysData([1, 0, 3], ['2026-11-01', '2026-11-02', '2026-11-03'])
    expect(data.map(d => d.isoDate)).toEqual(['2026-11-01', '2026-11-02', '2026-11-03'])
    expect(data.map(d => d.label)).toEqual(['Nov 1', 'Nov 2', 'Today'])
    expect(data.map(d => d.count)).toEqual([1, 0, 3])
  })

  it('falls back to UTC days counted back from now', () => {
    // 23:30 UTC on Nov 4 — must not jump to Nov 5 in a UTC+ zone or similar.
    const now = new Date(Date.UTC(2026, 10, 4, 23, 30))
    const data = buildRecentPlaysData([1, 2, 3], undefined, now)
    expect(data.map(d => d.isoDate)).toEqual(['2026-11-02', '2026-11-03', '2026-11-04'])
  })

  it('ignores recentPlayDays of the wrong length', () => {
    const now = new Date(Date.UTC(2026, 0, 1, 12))
    const data = buildRecentPlaysData([1, 2], ['2026-01-01'], now)
    expect(data.map(d => d.isoDate)).toEqual(['2025-12-31', '2026-01-01'])
  })
})

describe('useLeagueGameSummaries.loadMore', () => {
  beforeEach(() => { mockGet.mockReset() })

  it('only advances the page on success, so a failed load can be retried', async () => {
    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 1 }], totalCount: 3 } })
    const { result } = renderHook(() => useLeagueGameSummaries(5))
    await waitFor(() => expect(result.current.items).toHaveLength(1))

    mockGet.mockRejectedValueOnce(new Error('boom'))
    await act(async () => { result.current.loadMore() })
    await waitFor(() => expect(result.current.error).toBeTruthy())
    expect(mockGet.mock.calls[1][1].params.page).toBe(2)

    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 2 }], totalCount: 3 } })
    await act(async () => { result.current.loadMore() })
    await waitFor(() => expect(result.current.items).toHaveLength(2))
    expect(mockGet.mock.calls[2][1].params.page).toBe(2)
    expect(result.current.error).toBeNull()

    mockGet.mockResolvedValueOnce({ data: { items: [{ gameId: 3 }], totalCount: 3 } })
    await act(async () => { result.current.loadMore() })
    await waitFor(() => expect(result.current.items).toHaveLength(3))
    expect(mockGet.mock.calls[3][1].params.page).toBe(3)
  })
})
