import React from 'react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, act, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const adapterListeners = new Set()
const mockHasAdapterForUrl = vi.fn()
vi.mock('../utils/adapters', () => ({
  hasAdapterForUrl: (...a) => mockHasAdapterForUrl(...a),
  subscribeAdapters: listener => {
    adapterListeners.add(listener)
    return () => adapterListeners.delete(listener)
  },
}))

import PlayButton from '../components/ui/PlayButton'
import AppButton from '../components/ui/AppButton'
import ChartTooltipLabel from '../components/ui/ChartTooltipLabel'
import Countdown from '../components/Countdown'
import ImageUpload from '../components/ui/ImageUpload/ImageUpload'
import SubmissionCard from '../components/SubmissionCard'

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: { id: 5 } }),
}))

const game = { id: 7, url: 'https://game.example/daily' }
const inRouter = ui => render(<MemoryRouter>{ui}</MemoryRouter>)

describe('PlayButton', () => {
  beforeEach(() => {
    mockHasAdapterForUrl.mockReset()
    adapterListeners.clear()
  })

  it('links into the in-app Play flow when the adapter is known', () => {
    inRouter(<PlayButton game={game} adapter={{ id: 'x' }} />)

    expect(screen.getByRole('link', { name: 'Play' })).toHaveAttribute('href', '/play/7')
    expect(mockHasAdapterForUrl).not.toHaveBeenCalled()
  })

  it('opens the game in a new tab without an adapter', () => {
    inRouter(<PlayButton game={game} adapter={null}>Go</PlayButton>)

    const link = screen.getByRole('link', { name: 'Go' })
    expect(link).toHaveAttribute('href', game.url)
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('renders nothing for an unsafe URL', () => {
    const { container } = inRouter(<PlayButton game={{ id: 1, url: 'javascript:alert(1)' }} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('asks the extension when no adapter is passed', async () => {
    mockHasAdapterForUrl.mockResolvedValue(true)

    inRouter(<PlayButton game={game} />)

    await waitFor(() => expect(screen.getByRole('link', { name: 'Play' })).toHaveAttribute('href', '/play/7'))
    expect(mockHasAdapterForUrl).toHaveBeenCalledWith(game.url)
  })

  it('falls back to the new tab when the lookup fails', async () => {
    mockHasAdapterForUrl.mockRejectedValue(new Error('no'))

    inRouter(<PlayButton game={game} />)

    await waitFor(() => expect(mockHasAdapterForUrl).toHaveBeenCalled())
    expect(screen.getByRole('link', { name: 'Play' })).toHaveAttribute('href', game.url)
  })

  it('looks again when the extension adapters change', async () => {
    mockHasAdapterForUrl.mockResolvedValueOnce(false).mockResolvedValueOnce(true)
    inRouter(<PlayButton game={game} />)
    await waitFor(() => expect(mockHasAdapterForUrl).toHaveBeenCalledTimes(1))

    act(() => adapterListeners.forEach(listener => listener([])))

    await waitFor(() => expect(screen.getByRole('link', { name: 'Play' })).toHaveAttribute('href', '/play/7'))
  })

  it('does not subscribe when the adapter is passed in', () => {
    inRouter(<PlayButton game={game} adapter={null} />)

    expect(adapterListeners.size).toBe(0)
  })
})

describe('ChartTooltipLabel', () => {
  it('shows white text on a translucent black label', () => {
    render(<ChartTooltipLabel>Today: 3 plays</ChartTooltipLabel>)

    expect(screen.getByText('Today: 3 plays')).toHaveStyle({ background: 'rgba(0,0,0,0.75)', color: '#fff' })
  })
})

describe('AppButton', () => {
  it('renders a router link for `to`', () => {
    inRouter(<AppButton to="/games">Games</AppButton>)

    expect(screen.getByRole('link', { name: 'Games' })).toHaveAttribute('href', '/games')
  })

  it('defaults rel for a link opened in a new tab', () => {
    inRouter(<AppButton href="https://x.example" target="_blank">X</AppButton>)

    expect(screen.getByRole('link', { name: 'X' })).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('keeps an explicit rel', () => {
    inRouter(<AppButton href="https://x.example" target="_blank" rel="nofollow">X</AppButton>)

    expect(screen.getByRole('link', { name: 'X' })).toHaveAttribute('rel', 'nofollow')
  })

  it('passes rel and drops target-less defaults for a plain href', () => {
    inRouter(<AppButton href="https://x.example" rel="me">X</AppButton>)

    const link = screen.getByRole('link', { name: 'X' })
    expect(link).toHaveAttribute('rel', 'me')
    expect(link).not.toHaveAttribute('target')
  })

  it('renders a button with a data-test attribute by default', () => {
    inRouter(<AppButton dataTest="save">Save</AppButton>)

    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('data-test', 'save')
  })

  it('uses an explicit component', () => {
    inRouter(<AppButton component="span">Span</AppButton>)

    expect(screen.getByText('Span').closest('span.MuiButton-root')).not.toBeNull()
  })
})

describe('Countdown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T10:00:00Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('counts down to the target as hh:mm:ss', () => {
    render(<Countdown targetDate="2026-01-01T12:03:04Z" />)

    expect(screen.getByText('Resets in 02:03:04')).toBeInTheDocument()

    act(() => { vi.advanceTimersByTime(1000) })

    expect(screen.getByText('Resets in 02:03:03')).toBeInTheDocument()
  })

  it('renders nothing for an invalid target', () => {
    const { container } = render(<Countdown targetDate="nonsense" />)

    expect(container).toBeEmptyDOMElement()
  })

  it('says the reset is complete at the target, before calling onExpire', () => {
    const onExpire = vi.fn()
    render(<Countdown targetDate="2026-01-01T10:00:02Z" onExpire={onExpire} />)

    act(() => { vi.advanceTimersByTime(2000) })

    expect(screen.getByText('Reset complete!')).toBeInTheDocument()
    expect(onExpire).not.toHaveBeenCalled()
  })

  it('calls onExpire after a grace period', () => {
    const onExpire = vi.fn()
    render(<Countdown targetDate="2026-01-01T10:00:02Z" onExpire={onExpire} />)
    act(() => { vi.advanceTimersByTime(2000) })

    act(() => { vi.advanceTimersByTime(1500) })

    expect(onExpire).toHaveBeenCalledTimes(1)
  })

  it('calls onExpire only once', () => {
    const onExpire = vi.fn()
    render(<Countdown targetDate="2026-01-01T10:00:02Z" onExpire={onExpire} />)
    act(() => { vi.advanceTimersByTime(2000) })
    act(() => { vi.advanceTimersByTime(1500) })

    act(() => { vi.advanceTimersByTime(10000) })

    expect(onExpire).toHaveBeenCalledTimes(1)
  })
})

describe('ImageUpload', () => {
  it('shows the picked file name with a preview and clears the input on remove', () => {
    const onFileChange = vi.fn()
    const onRemove = vi.fn()
    const { container, rerender } = render(<ImageUpload onFileChange={onFileChange} onRemove={onRemove} />)
    expect(screen.getByText('No file selected.')).toBeInTheDocument()
    const input = container.querySelector('input[type="file"]')

    fireEvent.change(input, { target: { files: [new File(['x'], 'shot.png', { type: 'image/png' })] } })
    rerender(<ImageUpload onFileChange={onFileChange} onRemove={onRemove} previewUrl="blob:x" />)

    expect(onFileChange).toHaveBeenCalled()
    expect(screen.getByText('shot.png')).toBeInTheDocument()
  })

  it('says an image is attached when the preview comes from elsewhere', () => {
    render(<ImageUpload previewUrl="blob:x" />)

    expect(screen.getByText('Image attached.')).toBeInTheDocument()
  })
})

describe('SubmissionCard', () => {
  const submission = { id: 11, userId: 5, username: 'me', score: 1234, rank: 2, screenshotUrl: '/uploads/a.png', createdAt: '2026-01-01T10:00:00Z' }

  it('links a visible submission to its detail page', () => {
    inRouter(<SubmissionCard submission={submission} />)

    expect(screen.getAllByRole('link').find(a => a.getAttribute('href') === '/submission/11')).toBeTruthy()
    expect(screen.getByText('#2')).toBeInTheDocument()
    expect(screen.getByText('You')).toBeInTheDocument()
    expect(screen.getByAltText('screenshot')).toBeInTheDocument()
  })

  it('shows a masked submission as hidden, without screenshot or link', () => {
    inRouter(<SubmissionCard submission={{ ...submission, score: null, screenshotUrl: null, rank: null }} />)

    expect(screen.getByText('Hidden')).toBeInTheDocument()
    expect(screen.queryByAltText('screenshot')).toBeNull()
    expect(screen.getAllByRole('link').some(a => a.getAttribute('href') === '/submission/11')).toBe(false)
  })

  it('toggles comparison instead of navigating in compare mode', () => {
    const onToggleCompare = vi.fn()
    inRouter(<SubmissionCard submission={submission} compareMode selected={false} onToggleCompare={onToggleCompare} />)

    fireEvent.click(screen.getByRole('checkbox'))

    expect(onToggleCompare).toHaveBeenCalledWith(submission)
  })

  it('renders nothing without a submission', () => {
    const { container } = inRouter(<SubmissionCard submission={null} />)

    expect(container).toBeEmptyDOMElement()
  })
})
