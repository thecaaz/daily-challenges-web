import React from 'react'
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import LeagueLeaderboardTable from '../components/League/LeagueLeaderboardTable'

function renderTable(leaderboard) {
  return render(
    <MemoryRouter>
      <LeagueLeaderboardTable leaderboard={leaderboard} currentUserId={1} />
    </MemoryRouter>
  )
}

const visibleLeaderboard = {
  scoresHidden: false,
  entries: [
    { rank: 1, userId: 2, username: 'boss', score: 900, screenshotUrl: '/shots/a.png', submissionId: 55 },
    { rank: 2, userId: 1, username: 'me', score: 700 },
    { rank: 3, userId: 3, username: 'late', score: null },
  ],
}

describe('LeagueLeaderboardTable', () => {
  it('shows ranks, scores and a screenshot link for visible entries', () => {
    renderTable(visibleLeaderboard)

    expect(screen.getByText('900')).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'boss screenshot' }).closest('a')).toHaveAttribute('href', '/submission/55')
    expect(screen.getByRole('link', { name: 'me' })).toHaveAttribute('href', '/users/1')
  })

  it('masks an entry without a score', () => {
    renderTable(visibleLeaderboard)

    const lateRow = screen.getByRole('link', { name: 'late' }).closest('tr')
    expect(lateRow).toHaveTextContent('–')
    expect(lateRow).toHaveTextContent('Hidden')
  })

  it('masks every entry when the day is hidden', () => {
    renderTable({ ...visibleLeaderboard, scoresHidden: true })

    expect(screen.getAllByText('Hidden')).toHaveLength(3)
    expect(screen.queryByRole('img')).toBeNull()
  })
})
