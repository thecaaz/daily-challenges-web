import React from 'react'
import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

vi.mock('../api', () => ({ default: {}, getApiRoot: () => '' }))
vi.mock('../components/League/RecentPlaysBar', () => ({
  default: ({ onDayClick }) => <span role="button" onClick={() => onDayClick('2026-11-03')}>Recent plays</span>,
}))

import LeagueGameCard from '../components/League/LeagueGameCard'

function summary(overrides = {}) {
  return {
    gameId: 3,
    gameName: 'Wordle',
    iconUrl: null,
    lastPlayedAt: null,
    playCount: 1,
    topScore: null,
    myBestScore: null,
    myRank: null,
    recentPlays: [],
    recentPlayDays: [],
    ...overrides,
  }
}

function renderCard(props) {
  const onSelect = vi.fn()
  render(<MemoryRouter><LeagueGameCard summary={summary(props)} onSelect={onSelect} /></MemoryRouter>)
  return onSelect
}

describe('LeagueGameCard', () => {
  it('shows a placeholder icon, singular play count and not-played texts for a fresh game', () => {
    renderCard()

    expect(screen.getByText('🎮')).toBeInTheDocument()
    expect(screen.getByText('1 play')).toBeInTheDocument()
    expect(screen.getByText('My best: Not played')).toBeInTheDocument()
    expect(screen.getByText('Not played recently')).toBeInTheDocument()
    expect(screen.queryByText(/rank$/)).not.toBeInTheDocument()
    expect(screen.queryByText(/^Top:/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Recent plays' })).not.toBeInTheDocument()
  })

  it('shows the game icon, stats and my best score once played', () => {
    renderCard({ iconUrl: '/icons/w.png', playCount: 1200, topScore: 99, myBestScore: 42, myRank: 2, lastPlayedAt: new Date().toISOString() })

    expect(screen.getByAltText('Wordle')).toBeInTheDocument()
    expect(screen.getByText('#2 rank')).toBeInTheDocument()
    expect(screen.getByText('Top: 99')).toBeInTheDocument()
    expect(screen.getByText(/plays$/)).toBeInTheDocument()
    expect(screen.getByText('My best: 42')).toBeInTheDocument()
    expect(screen.getByText(/^Last played /)).toBeInTheDocument()
    expect(screen.queryByTestId('EmojiEventsIcon')).not.toBeInTheDocument()
  })

  it('marks the top-ranked player with a trophy', () => {
    renderCard({ myRank: 1 })

    expect(screen.getByText('#1 rank')).toBeInTheDocument()
    expect(screen.getByTestId('EmojiEventsIcon')).toBeInTheDocument()
  })

  it('links the game name to the game page', () => {
    renderCard()

    expect(screen.getByRole('link', { name: 'Wordle' })).toHaveAttribute('href', '/games/3')
  })

  it('selects the game when the card is clicked', () => {
    const onSelect = renderCard()

    fireEvent.click(screen.getByText('My best: Not played'))

    expect(onSelect).toHaveBeenCalledWith(3)
  })

  it('selects the game on a day when a recent-plays bar is clicked', () => {
    const onSelect = renderCard({ recentPlays: [1, 2] })

    fireEvent.click(screen.getByRole('button', { name: 'Recent plays' }))

    expect(onSelect).toHaveBeenCalledTimes(1)
    expect(onSelect).toHaveBeenCalledWith(3, '2026-11-03')
  })
})
