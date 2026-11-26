import React from 'react'
import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material/styles'

vi.mock('../api', () => ({ default: {}, getApiRoot: () => '' }))
vi.mock('../components/League/RecentPlaysBar', () => ({ default: () => null }))
vi.mock('../hooks/useReleaseInfo', () => ({ default: () => ({ version: '1.0.0' }) }))

import createAppTheme from '../theme'
import ChangelogModal from '../components/ChangelogModal'
import BrandLogo from '../components/AppHeader/BrandLogo'
import GameIframePlayer from '../components/ui/GameIframePlayer'
import LeagueGameCard from '../components/League/LeagueGameCard'
import Countdown from '../components/Countdown'
import VersionBadge from '../components/VersionBadge'

const inTheme = (mode, ui) => render(<ThemeProvider theme={createAppTheme(mode)}><MemoryRouter>{ui}</MemoryRouter></ThemeProvider>)

describe('style characterization', () => {
  it.each(['light', 'dark'])('keeps the changelog code block background in %s mode', mode => {
    inTheme(mode, <ChangelogModal open onClose={() => {}} version="1.0.0" changelog={'```\ncode\n```'} />)

    expect(document.querySelector('pre').parentElement.className).toMatchSnapshot()
  })

  it('keeps the brand logo styles', () => {
    const { container } = inTheme('light', <BrandLogo />)

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('keeps the maximized game player styles', () => {
    const { container } = inTheme('light', <GameIframePlayer src="https://game.example/" title="Game" isMaximized onRestore={() => {}} />)

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('keeps the inline game player styles', () => {
    const { container } = inTheme('light', <GameIframePlayer src="https://game.example/" title="Game" isMaximized={false} onRestore={() => {}} />)

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('shows the game player iframe once it has loaded', () => {
    const { container } = inTheme('light', <GameIframePlayer src="https://game.example/" title="Game" isMaximized={false} onRestore={() => {}} />)

    fireEvent.load(screen.getByTitle('Game'))

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('keeps the league game card placeholder icon', () => {
    const summary = { gameId: 3, gameName: 'Wordle', iconUrl: null, playCount: 1, recentPlays: [], recentPlayDays: [] }

    inTheme('light', <LeagueGameCard summary={summary} onSelect={() => {}} />)

    expect(screen.getByText('🎮').className).toMatchSnapshot()
  })

  it('keeps the version badge outline', () => {
    inTheme('light', <VersionBadge />)

    expect(screen.getByRole('button', { name: '1.0.0' }).className).toMatchSnapshot()
  })

  it('keeps the countdown caption colour', () => {
    inTheme('light', <Countdown targetDate={new Date(Date.now() + 3_600_000).toISOString()} />)

    expect(screen.getByText(/Resets in/).className).toMatchSnapshot()
  })
})
