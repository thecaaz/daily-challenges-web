import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, act, waitFor } from '@testing-library/react'

const mockRefresh = vi.fn()
const mockGet = vi.fn()
const mockPost = vi.fn()
const mockSetAccessToken = vi.fn()

vi.mock('../api', () => ({
  default: { get: (...a) => mockGet(...a), post: (...a) => mockPost(...a) },
  refreshSession: (...a) => mockRefresh(...a),
  setAccessToken: (...a) => mockSetAccessToken(...a),
  AUTH_EXPIRED_EVENT: 'auth:expired',
}))

import { AuthProvider, useAuth } from '../contexts/AuthContext'

const firstRenderUsers = []
function httpError(status) {
  return Object.assign(new Error(String(status)), { response: { status } })
}

function Child() {
  const { user, loading } = useAuth()
  const seen = React.useRef(false)
  if (!seen.current) { seen.current = true; firstRenderUsers.push({ user, loading }) }
  return <div data-testid="child">{user ? `hello ${user.username}` : 'anonymous'}</div>
}

function LogoutButton() {
  const { logout } = useAuth()
  return <button onClick={() => logout().catch(() => {})}>logout</button>
}

describe('AuthProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    firstRenderUsers.length = 0
  })

  it('shows a loading screen instead of children while the session is being restored', () => {
    mockRefresh.mockImplementation(() => new Promise(() => {}))

    render(<AuthProvider><Child /></AuthProvider>)

    expect(screen.queryByTestId('child')).toBeNull()
    expect(screen.getByText('Loading…')).toBeInTheDocument()
  })

  it('gives children the restored user from their very first render', async () => {
    let finishRefresh
    mockRefresh.mockImplementation(() => new Promise(r => { finishRefresh = r }))
    mockGet.mockResolvedValue({ data: { id: 1, username: 'alice' } })
    render(<AuthProvider><Child /></AuthProvider>)

    await act(async () => { finishRefresh('token') })

    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('hello alice'))
    expect(firstRenderUsers).toEqual([{ user: { id: 1, username: 'alice' }, loading: false }])
    expect(mockGet).toHaveBeenCalledWith('/auth/me')
  })

  it('renders anonymously when there is no session, without calling /auth/me', async () => {
    mockRefresh.mockRejectedValue(httpError(401))

    render(<AuthProvider><Child /></AuthProvider>)

    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('anonymous'))
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('clears the user when api.js reports auth:expired', async () => {
    mockRefresh.mockResolvedValue('token')
    mockGet.mockResolvedValue({ data: { id: 1, username: 'alice' } })

    render(<AuthProvider><Child /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('hello alice'))

    act(() => { window.dispatchEvent(new Event('auth:expired')) })

    expect(screen.getByTestId('child')).toHaveTextContent('anonymous')
    expect(mockSetAccessToken).toHaveBeenLastCalledWith(null)
  })

  it('logout clears local state even when the request fails', async () => {
    mockRefresh.mockResolvedValue('token')
    mockGet.mockResolvedValue({ data: { id: 1, username: 'alice' } })
    mockPost.mockRejectedValue(new Error('Network Error'))

    render(<AuthProvider><Child /><LogoutButton /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('hello alice'))

    await act(async () => { screen.getByText('logout').click() })

    expect(screen.getByTestId('child')).toHaveTextContent('anonymous')
    expect(mockSetAccessToken).toHaveBeenLastCalledWith(null)
  })

  it('fetchMe keeps the user on a non-401 failure', async () => {
    mockRefresh.mockResolvedValue('token')
    mockGet.mockResolvedValueOnce({ data: { id: 1, username: 'alice' } })

    let ctx
    function Grab() { ctx = useAuth(); return null }
    render(<AuthProvider><Child /><Grab /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('hello alice'))

    mockGet.mockRejectedValueOnce(httpError(500))
    await act(async () => { await ctx.fetchMe() })
    expect(screen.getByTestId('child')).toHaveTextContent('hello alice')

    mockGet.mockRejectedValueOnce(httpError(401))
    await act(async () => { await ctx.fetchMe() })
    expect(screen.getByTestId('child')).toHaveTextContent('anonymous')
  })

  it('login stores the access token and loads the user', async () => {
    mockRefresh.mockRejectedValue(new Error('no session'))
    mockPost.mockResolvedValue({ data: { accessToken: 'tok', extra: 1 } })
    mockGet.mockResolvedValue({ data: { id: 2, username: 'bob' } })
    let ctx
    function Grab() { ctx = useAuth(); return null }
    render(<AuthProvider><Child /><Grab /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('anonymous'))

    let result
    await act(async () => { result = await ctx.login('bob', 'pw') })

    expect(mockPost).toHaveBeenCalledWith('/auth/login', { username: 'bob', password: 'pw' })
    expect(mockSetAccessToken).toHaveBeenCalledWith('tok')
    expect(result).toEqual({ accessToken: 'tok', extra: 1 })
    expect(screen.getByTestId('child')).toHaveTextContent('hello bob')
  })

  it('login accepts a PascalCase AccessToken and skips storing a missing one', async () => {
    mockRefresh.mockRejectedValue(new Error('no session'))
    mockGet.mockResolvedValue({ data: { id: 2, username: 'bob' } })
    let ctx
    function Grab() { ctx = useAuth(); return null }
    render(<AuthProvider><Child /><Grab /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('anonymous'))

    mockPost.mockResolvedValueOnce({ data: { AccessToken: 'TOK' } })
    await act(async () => { await ctx.login('bob', 'pw') })
    mockPost.mockResolvedValueOnce({ data: {} })
    await act(async () => { await ctx.login('bob', 'pw') })

    expect(mockSetAccessToken.mock.calls).toEqual([['TOK']])
  })

  it('register posts the credentials and returns the response body', async () => {
    mockRefresh.mockRejectedValue(new Error('no session'))
    mockPost.mockResolvedValue({ data: { id: 3 } })
    let ctx
    function Grab() { ctx = useAuth(); return null }
    render(<AuthProvider><Grab /></AuthProvider>)
    await waitFor(() => expect(ctx.loading).toBe(false))

    const result = await ctx.register('carol', 'pw')

    expect(mockPost).toHaveBeenCalledWith('/auth/register', { username: 'carol', password: 'pw' })
    expect(result).toEqual({ id: 3 })
  })
})
