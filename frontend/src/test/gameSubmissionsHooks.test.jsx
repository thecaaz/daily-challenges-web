import React from 'react'
import { describe, it, expect } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import useCompareSelection from '../hooks/useCompareSelection'
import useSelectedScoringDay from '../hooks/useSelectedScoringDay'

function routerAt(path) {
  return ({ children }) => <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
}

describe('useCompareSelection', () => {
  it('drops the oldest pick when a third submission is selected', () => {
    const { result } = renderHook(() => useCompareSelection('list-1'), { wrapper: routerAt('/') })

    act(() => { result.current.toggleMode() })
    act(() => { result.current.toggle({ id: 1 }) })
    act(() => { result.current.toggle({ id: 2 }) })
    act(() => { result.current.toggle({ id: 3 }) })

    expect([1, 2, 3].map(result.current.isSelected)).toEqual([false, true, true])
    expect(result.current.canCompare).toBe(true)
  })

  it('clears the selection but stays in compare mode when the list changes', () => {
    const { result, rerender } = renderHook(({ listKey }) => useCompareSelection(listKey), {
      wrapper: routerAt('/'),
      initialProps: { listKey: 'list-1' },
    })
    act(() => { result.current.toggleMode() })
    act(() => { result.current.toggle({ id: 1 }) })

    rerender({ listKey: 'list-2' })

    expect(result.current.isSelected(1)).toBe(false)
    expect(result.current.isComparing).toBe(true)
  })
})

describe('useSelectedScoringDay', () => {
  it.each([
    ['/games/1', '2026-11-04'],
    ['/games/1?scoringDay=2026-11-01', '2026-11-01'],
    ['/games/1?scoringDay=all', ''],
  ])('reads %s as day "%s"', (path, expectedDay) => {
    const { result } = renderHook(() => useSelectedScoringDay('2026-11-04'), { wrapper: routerAt(path) })

    expect(result.current.selectedDate).toBe(expectedDay)
  })

  it('stores "all days" as scoringDay=all', () => {
    const { result } = renderHook(() => useSelectedScoringDay('2026-11-04'), { wrapper: routerAt('/games/1') })

    act(() => { result.current.selectDate('') })

    expect(result.current.selectedDate).toBe('')
  })
})
