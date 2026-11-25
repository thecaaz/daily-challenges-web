import { useState, useEffect, useCallback } from 'react'
import compressImage from '../utils/compressImage'

const IMAGE_TYPE_PREFIX = 'image/'
const CLIPBOARD_FILENAME = 'clipboard.png'

/**
 * A screenshot picked from a file input or pasted from the clipboard
 * (Ctrl+V), compressed when possible, with an object-URL preview that is
 * revoked when the screenshot changes. A paste with an image is claimed
 * (preventDefault) before the first await, while the event is still dispatching.
 *
 * @param {Function} [showSnackbar]
 * @param {object} [options]
 * @param {boolean} [options.enabled=true] - listen for clipboard pastes. Pass
 *   false for forms that aren't active so a paste only fills one form.
 */
export default function useImageUpload(showSnackbar, { enabled = true } = {}) {
  const [screenshot, setScreenshot] = useState(null)
  const previewUrl = useObjectUrl(screenshot)

  const onFileChange = useCallback(async (e) => {
    const raw = e.target.files?.[0] ?? null
    if (!raw) {
      setScreenshot(null)
      return
    }
    setScreenshot(await compressOr(raw, raw))
  }, [])

  useEffect(() => {
    if (!enabled) return undefined
    const handlePaste = async (e) => {
      const image = firstClipboardImage(e.clipboardData?.items)
      if (!image) return
      e.preventDefault()
      setScreenshot(await compressOr(image, asClipboardFile(image)))
      if (typeof showSnackbar === 'function') showSnackbar('Image pasted from clipboard', 'success')
    }
    window.addEventListener('paste', handlePaste)
    return () => window.removeEventListener('paste', handlePaste)
  }, [showSnackbar, enabled])

  const clear = useCallback(() => setScreenshot(null), [])

  return { screenshot, previewUrl, setScreenshot, onFileChange, clear }
}

function useObjectUrl(blob) {
  const [url, setUrl] = useState(null)

  useEffect(() => {
    if (!blob) {
      setUrl(null)
      return
    }
    const objectUrl = URL.createObjectURL(blob)
    setUrl(objectUrl)
    return () => { URL.revokeObjectURL(objectUrl) }
  }, [blob])

  return url
}

async function compressOr(image, fallback) {
  try {
    return await compressImage(image)
  } catch {
    return fallback
  }
}

/** Reads the clipboard synchronously: its data is gone after the paste event. */
function firstClipboardImage(items) {
  if (!items) return null
  for (const item of Array.from(items)) {
    const blob = item?.type?.startsWith(IMAGE_TYPE_PREFIX) && item.getAsFile ? item.getAsFile() : null
    if (blob) return blob
  }
  return null
}

function asClipboardFile(blob) {
  return new File([blob], CLIPBOARD_FILENAME, { type: blob.type })
}
