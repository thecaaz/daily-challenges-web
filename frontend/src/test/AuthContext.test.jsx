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

// Records the user seen on the child's very first render.
const firstRenderUsers = []
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

  it('renders children only after the session restore finished', async () => {
    let finishRefresh
    mockRefresh.mockImplementation(() => new Promise(r => { finishRefresh = r }))
    mockGet.mockResolvedValue({ data: { id: 1, username: 'alice' } })

    render(<AuthProvider><Child /></AuthProvider>)

    expect(screen.queryByTestId('child')).toBeNull()
    expect(screen.getByText('Loading…')).toBeInTheDocument()

    await act(async () => { finishRefresh('token') })

    await waitFor(() => expect(screen.getByTestId('child')).toHaveTextContent('hello alice'))
    // The very first render of the app already had the restored user.
    expect(firstRenderUsers).toEqual([{ user: { id: 1, username: 'alice' }, loading: false }])
    expect(mockGet).toHaveBeenCalledWith('/auth/me')
  })

  it('renders anonymously when there is no session, without calling /auth/me', async () => {
    mockRefresh.mockRejectedValue(Object.assign(new Error('401'), { response: { status: 401 } }))

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

    mockGet.mockRejectedValueOnce(Object.assign(new Error('500'), { response: { status: 500 } }))
    await act(async () => { await ctx.fetchMe() })
    expect(screen.getByTestId('child')).toHaveTextContent('hello alice')

    mockGet.mockRejectedValueOnce(Object.assign(new Error('401'), { response: { status: 401 } }))
    await act(async () => { await ctx.fetchMe() })
    expect(screen.getByTestId('child')).toHaveTextContent('anonymous')
  })
})
