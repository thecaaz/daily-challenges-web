import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const mockLogin = vi.fn()
const mockRegister = vi.fn()
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ login: mockLogin, register: mockRegister }),
}))
const mockShowSnackbar = vi.fn()
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => ({ showSnackbar: mockShowSnackbar }),
}))

import Login from '../pages/Login'
import Register from '../pages/Register'

function renderAt(path) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/" element={<div>Home page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

const usernameField = () => screen.getByLabelText(/Username/)
const passwordField = () => screen.getByLabelText(/Password/)

function fillIn(username, password) {
  fireEvent.change(usernameField(), { target: { value: username } })
  fireEvent.change(passwordField(), { target: { value: password } })
}

const submit = name => fireEvent.click(screen.getByRole('button', { name }))

beforeEach(() => {
  mockLogin.mockReset()
  mockRegister.mockReset()
  mockShowSnackbar.mockReset()
})

describe('Login', () => {
  it('renders the same form as before the refactor', () => {
    const { container } = renderAt('/login')

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('asks for both fields when they are blank', () => {
    renderAt('/login')
    fillIn('   ', '')

    fireEvent.submit(usernameField().closest('form'))

    expect(screen.getByText('Enter your username')).toBeInTheDocument()
    expect(screen.getByText('Enter your password')).toBeInTheDocument()
    expect(mockLogin).not.toHaveBeenCalled()
  })

  it('accepts short legacy credentials and trims the username', async () => {
    mockLogin.mockResolvedValue()
    renderAt('/login')
    fillIn('  ab  ', 'x')

    submit('Login')

    await waitFor(() => expect(screen.getByText('Home page')).toBeInTheDocument())
    expect(mockLogin).toHaveBeenCalledWith('ab', 'x')
  })

  it('clears a field error when the field is edited', () => {
    renderAt('/login')
    fireEvent.submit(usernameField().closest('form'))

    fireEvent.change(usernameField(), { target: { value: 'a' } })

    expect(screen.queryByText('Enter your username')).toBeNull()
    expect(screen.getByText('Enter your password')).toBeInTheDocument()
  })

  it('shows the loading label while logging in', async () => {
    let finishLogin
    mockLogin.mockReturnValue(new Promise(resolve => { finishLogin = resolve }))
    renderAt('/login')
    fillIn('alice', 'secret')

    submit('Login')

    expect(await screen.findByRole('button', { name: 'Logging in…' })).toBeDisabled()
    finishLogin()
    await waitFor(() => expect(screen.getByText('Home page')).toBeInTheDocument())
  })

  it('reports a failed login', async () => {
    mockLogin.mockRejectedValue(new Error('nope'))
    renderAt('/login')
    fillIn('alice', 'secret')

    submit('Login')

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith(expect.any(String), 'error'))
    expect(screen.getByRole('button', { name: 'Login' })).toBeEnabled()
  })
})

describe('Register', () => {
  it('renders the same form as before the refactor', () => {
    const { container } = renderAt('/register')

    expect(container.innerHTML).toMatchSnapshot()
  })

  it('shows the field rules as hints', () => {
    renderAt('/register')

    expect(screen.getByText('3–32 characters: letters, digits, _ . -')).toBeInTheDocument()
    expect(screen.getByText('8–128 characters')).toBeInTheDocument()
    expect(usernameField()).toHaveAttribute('maxLength', '32')
    expect(passwordField()).toHaveAttribute('maxLength', '128')
  })

  it('rejects a username and password outside the length rules', () => {
    renderAt('/register')
    fillIn('ab', 'short')

    submit('Register')

    expect(screen.getByText('Username must be 3–32 characters')).toBeInTheDocument()
    expect(screen.getByText('Password must be 8–128 characters')).toBeInTheDocument()
    expect(mockRegister).not.toHaveBeenCalled()
  })

  it('rejects characters outside the username alphabet', () => {
    renderAt('/register')
    fillIn('bad name', 'longenough')

    submit('Register')

    expect(screen.getByText('Use only letters, digits, "_", "." and "-"')).toBeInTheDocument()
  })

  it('creates the account with the trimmed username', async () => {
    mockRegister.mockResolvedValue()
    renderAt('/register')
    fillIn(' new.user ', 'longenough')

    submit('Register')

    await waitFor(() => expect(mockRegister).toHaveBeenCalledWith('new.user', 'longenough'))
    expect(mockShowSnackbar).toHaveBeenCalledWith('Account created! Please log in.', 'success')
  })
})
