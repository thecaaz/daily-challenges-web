import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { renderHook, act, waitFor } from '@testing-library/react'
import useImageUpload from '../hooks/useImageUpload'
import compressImage from '../utils/compressImage'

vi.mock('../utils/compressImage', () => ({ default: vi.fn() }))

const imageFile = () => new File(['x'], 'shot.png', { type: 'image/png' })
const compressed = new File(['y'], 'capture.webp', { type: 'image/webp' })

function pasteEvent(items) {
  const event = new Event('paste', { cancelable: true })
  event.clipboardData = { items }
  return event
}

const clipboardImage = blob => ({ type: blob.type, getAsFile: () => blob })

describe('useImageUpload', () => {
  let createObjectURL
  let revokeObjectURL

  beforeEach(() => {
    compressImage.mockReset()
    compressImage.mockResolvedValue(compressed)
    createObjectURL = URL.createObjectURL
    revokeObjectURL = URL.revokeObjectURL
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
  })

  afterEach(() => {
    URL.createObjectURL = createObjectURL
    URL.revokeObjectURL = revokeObjectURL
  })

  it('compresses a picked file and previews it', async () => {
    const { result } = renderHook(() => useImageUpload())

    await act(() => result.current.onFileChange({ target: { files: [imageFile()] } }))

    expect(result.current.screenshot).toBe(compressed)
    expect(result.current.previewUrl).toBe('blob:preview')
  })

  it('keeps the original file when compression fails', async () => {
    compressImage.mockRejectedValue(new Error('nope'))
    const raw = imageFile()
    const { result } = renderHook(() => useImageUpload())

    await act(() => result.current.onFileChange({ target: { files: [raw] } }))

    expect(result.current.screenshot).toBe(raw)
  })

  it('clears the screenshot when no file was picked', async () => {
    const { result } = renderHook(() => useImageUpload())
    act(() => result.current.setScreenshot(imageFile()))

    await act(() => result.current.onFileChange({ target: { files: [] } }))

    expect(result.current.screenshot).toBeNull()
    expect(result.current.previewUrl).toBeNull()
  })

  it('revokes the preview URL when the screenshot is cleared', () => {
    const { result } = renderHook(() => useImageUpload())
    act(() => result.current.setScreenshot(imageFile()))

    act(() => result.current.clear())

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview')
    expect(result.current.screenshot).toBeNull()
  })

  it('accepts a pasted image, compresses it and says so', async () => {
    const showSnackbar = vi.fn()
    const { result } = renderHook(() => useImageUpload(showSnackbar))
    const event = pasteEvent([{ type: 'text/plain', getAsFile: () => null }, clipboardImage(imageFile())])

    act(() => { window.dispatchEvent(event) })

    expect(event.defaultPrevented).toBe(true)
    await waitFor(() => expect(result.current.screenshot).toBe(compressed))
    expect(showSnackbar).toHaveBeenCalledWith('Image pasted from clipboard', 'success')
  })

  it('wraps a pasted image in a File when compression fails', async () => {
    compressImage.mockRejectedValue(new Error('nope'))
    const blob = new Blob(['z'], { type: 'image/jpeg' })
    const { result } = renderHook(() => useImageUpload())

    act(() => { window.dispatchEvent(pasteEvent([clipboardImage(blob)])) })

    await waitFor(() => expect(result.current.screenshot).toBeInstanceOf(File))
    expect(result.current.screenshot.name).toBe('clipboard.png')
    expect(result.current.screenshot.type).toBe('image/jpeg')
  })

  it('ignores a paste without an image', () => {
    const showSnackbar = vi.fn()
    renderHook(() => useImageUpload(showSnackbar))
    const event = pasteEvent([{ type: 'text/plain', getAsFile: () => null }])

    act(() => { window.dispatchEvent(event) })

    expect(event.defaultPrevented).toBe(false)
    expect(compressImage).not.toHaveBeenCalled()
  })

  it('ignores a paste without clipboard data', () => {
    renderHook(() => useImageUpload())

    act(() => { window.dispatchEvent(new Event('paste')) })

    expect(compressImage).not.toHaveBeenCalled()
  })

  it('does not listen for pastes when disabled', () => {
    renderHook(() => useImageUpload(vi.fn(), { enabled: false }))

    act(() => { window.dispatchEvent(pasteEvent([clipboardImage(imageFile())])) })

    expect(compressImage).not.toHaveBeenCalled()
  })
})
