import React from 'react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const mockFetchMe = vi.fn()
let mockUser = { id: 7, level: 3 }
vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: mockUser, loading: false, fetchMe: mockFetchMe }),
}))

const mockShowSnackbar = vi.fn()
vi.mock('../contexts/SnackbarContext', () => ({
  useSnackbar: () => ({ showSnackbar: mockShowSnackbar }),
}))

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => ({
  ...(await importOriginal()),
  useNavigate: () => mockNavigate,
}))

const mockPost = vi.fn()
vi.mock('../api', () => ({
  default: { post: (...a) => mockPost(...a) },
  getApiRoot: () => '',
}))

vi.mock('../utils/compressImage', () => ({ default: vi.fn(async file => file) }))

import SubmissionForm from '../components/SubmissionForm/SubmissionForm'

const pngFile = (size = 10, type = 'image/png') => {
  const file = new File(['x'], 'shot.png', { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

function renderForm(props = {}) {
  return render(
    <MemoryRouter>
      <SubmissionForm gameId={42} initialScore="1234" initialScreenshot={pngFile()} {...props} />
    </MemoryRouter>
  )
}

const submit = () => fireEvent.click(screen.getByRole('button', { name: 'Submit' }))

describe('SubmissionForm', () => {
  let createObjectURL
  let revokeObjectURL

  beforeEach(() => {
    vi.clearAllMocks()
    mockUser = { id: 7, level: 3 }
    mockFetchMe.mockResolvedValue({ id: 7, level: 3 })
    mockPost.mockResolvedValue({ data: { submission: { id: 1 }, xpGain: 0 } })
    createObjectURL = URL.createObjectURL
    revokeObjectURL = URL.revokeObjectURL
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
  })

  afterEach(() => {
    URL.createObjectURL = createObjectURL
    URL.revokeObjectURL = revokeObjectURL
  })

  it('requires a screenshot', async () => {
    renderForm({ initialScreenshot: null })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Screenshot is required', 'error'))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('rejects a screenshot in an unsupported format', async () => {
    renderForm({ initialScreenshot: pngFile(10, 'image/gif') })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Allowed formats: PNG, JPEG, WebP', 'error'))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('rejects a screenshot larger than 2 MB', async () => {
    renderForm({ initialScreenshot: pngFile(2 * 1024 * 1024 + 1) })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Screenshot must be 2 MB or smaller', 'error'))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('requires a score', async () => {
    renderForm({ initialScore: '   ' })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Enter a score', 'error'))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('refuses a second submission for the same game', async () => {
    renderForm({ hasSubmitted: true })

    fireEvent.submit(screen.getByRole('button', { name: 'Submit' }).closest('form'))

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('You have already submitted for this game', 'error'))
    expect(screen.getByRole('button', { name: 'Submit' })).toBeDisabled()
  })

  it('warns about a non-numeric score and does not submit when the user declines', async () => {
    renderForm({ initialScore: 'abc' })
    expect(screen.getByText(/This score is not a number/)).toBeInTheDocument()

    submit()
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByText('Non-numeric score')).not.toBeInTheDocument())
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('submits a non-numeric score after confirmation', async () => {
    renderForm({ initialScore: 'abc', onSubmitted: vi.fn() })

    submit()
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Submit' }))

    await waitFor(() => expect(mockPost).toHaveBeenCalled())
  })

  it('posts the trimmed score and screenshot, then calls onSubmitted', async () => {
    const onSubmitted = vi.fn()
    mockPost.mockResolvedValue({ data: { submission: { id: 9 }, xpGain: 15 } })
    renderForm({ initialScore: ' 1234 ', onSubmitted })

    submit()

    await waitFor(() => expect(onSubmitted).toHaveBeenCalledWith({ submission: { id: 9 }, xpGain: 15 }))
    const [url, formData] = mockPost.mock.calls[0]
    expect(url).toBe('/submissions')
    expect(formData.get('gameId')).toBe('42')
    expect(formData.get('score')).toBe('1234')
    expect(formData.get('screenshot')).toBeInstanceOf(File)
    expect(mockShowSnackbar).toHaveBeenCalledWith('Submitted! +15 XP', 'success')
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('says "Submitted!" without XP', async () => {
    renderForm({ onSubmitted: vi.fn() })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Submitted!', 'success'))
  })

  it('announces a level-up with the XP gained', async () => {
    mockPost.mockResolvedValue({ data: { submission: {}, xpGain: 20 } })
    mockFetchMe.mockResolvedValue({ id: 7, level: 4 })
    renderForm({ onSubmitted: vi.fn() })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Submitted! +20 XP ⬆️ Level 4!', 'success'))
  })

  it('announces a level-up without XP', async () => {
    mockFetchMe.mockResolvedValue({ id: 7, level: 4 })
    renderForm({ onSubmitted: vi.fn() })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Level 4!', 'success'))
  })

  it('still reports success when refreshing the profile fails', async () => {
    mockPost.mockResolvedValue({ data: { submission: {}, xpGain: 5 } })
    mockFetchMe.mockRejectedValue(new Error('offline'))
    const onSubmitted = vi.fn()
    renderForm({ onSubmitted })

    submit()

    await waitFor(() => expect(onSubmitted).toHaveBeenCalled())
    expect(mockShowSnackbar).toHaveBeenCalledWith('Submitted! +5 XP', 'success')
  })

  it('navigates to the game without an onSubmitted handler', async () => {
    renderForm()

    submit()

    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/games/42'))
  })

  it('treats a 409 as a duplicate when the caller handles submissions', async () => {
    const onSubmitted = vi.fn()
    mockPost.mockRejectedValue({ response: { status: 409, data: { submission: { id: 3 }, xpGain: 0 } } })
    renderForm({ onSubmitted })

    submit()

    await waitFor(() => expect(onSubmitted).toHaveBeenCalledWith({ submission: { id: 3 }, xpGain: 0, duplicate: true }))
    expect(mockFetchMe).toHaveBeenCalled()
    expect(mockShowSnackbar).not.toHaveBeenCalledWith(expect.anything(), 'error')
  })

  it('shows the API error message when the submission fails', async () => {
    mockPost.mockRejectedValue({ response: { status: 400, data: { message: 'Bad score' } } })
    renderForm({ onSubmitted: vi.fn() })

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Bad score', 'error'))
  })

  it('shows the API error on a 409 without an onSubmitted handler', async () => {
    mockPost.mockRejectedValue({ response: { status: 409, data: { message: 'Already submitted' } } })
    renderForm()

    submit()

    await waitFor(() => expect(mockShowSnackbar).toHaveBeenCalledWith('Already submitted', 'error'))
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('cancel calls onCancel and clears the score', () => {
    const onCancel = vi.fn()
    renderForm({ onCancel })

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalled()
    expect(screen.getByLabelText(/Score/)).toHaveValue('')
  })

  it('follows a changed initialScore', () => {
    const { rerender } = renderForm()

    rerender(
      <MemoryRouter>
        <SubmissionForm gameId={42} initialScore="999" />
      </MemoryRouter>
    )

    expect(screen.getByLabelText(/Score/)).toHaveValue('999')
  })
})
