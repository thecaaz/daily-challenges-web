import React from 'react'
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import LeagueLeaderboardTable from '../components/League/LeagueLeaderboardTable'
import LeagueMembersList from '../components/League/LeagueMembersList'

const inRouter = ui => render(<MemoryRouter>{ui}</MemoryRouter>)

function expectPlainProfileLink(name, userId) {
  const link = screen.getByRole('link', { name })
  expect(link.tagName).toBe('A')
  expect(link).toHaveAttribute('href', `/users/${userId}`)
  expect(link).toHaveStyle({ textDecoration: 'none', color: 'inherit' })
}

describe('league user links', () => {
  it('renders leaderboard usernames as plain-looking profile links', () => {
    inRouter(
      <LeagueLeaderboardTable
        leaderboard={{ scoresHidden: false, entries: [{ rank: 1, userId: 7, username: 'boss', score: 900 }] }}
        currentUserId={1}
      />
    )

    expectPlainProfileLink('boss', 7)
  })

  it('renders member usernames as plain-looking profile links', () => {
    inRouter(
      <LeagueMembersList
        league={{ ownerId: 1, members: [{ userId: 8, username: 'member', role: 'member', level: 2, streak: 3 }] }}
        currentUserId={1}
        canRemoveMembers={false}
        onRemove={() => {}}
      />
    )

    expectPlainProfileLink('member', 8)
  })
})
