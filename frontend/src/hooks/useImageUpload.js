import { useState, useEffect, useCallback } from 'react'
import compressImage from '../utils/compressImage'

/**
 * @param {Function} [showSnackbar]
 * @param {object} [options]
 * @param {boolean} [options.enabled=true] - listen for clipboard pastes. Pass
 *   false for forms that aren't active so a paste only fills one form.
 */
export default function useImageUpload(showSnackbar, { enabled = true } = {}) {
  const [screenshot, setScreenshot] = useState(null)
  const [previewUrl, setPreviewUrl] = useState(null)

  // create/revoke preview URL when screenshot changes
  useEffect(() => {
    if (!screenshot) {
      setPreviewUrl(null)
      return
    }
    const url = URL.createObjectURL(screenshot)
    setPreviewUrl(url)
    return () => { URL.revokeObjectURL(url) }
  }, [screenshot])

  const onFileChange = useCallback(async (e) => {
    const raw = e.target.files?.[0] ?? null
    if (!raw) { setScreenshot(null); return }
    try {
      const compressed = await compressImage(raw)
      setScreenshot(compressed)
    } catch (_) {
      setScreenshot(raw)
    }
  }, [])

  // handle paste events (Ctrl+V) to accept images from clipboard
  useEffect(() => {
    if (!enabled) return undefined
    const handler = async (e) => {
      try {
        const items = e.clipboardData?.items
        if (!items) return
        for (let i = 0; i < items.length; i++) {
          const item = items[i]
          if (item && item.type && item.type.startsWith('image/')) {
            const blob = item.getAsFile ? item.getAsFile() : null
            if (blob) {
              // Must be called synchronously, before the first await.
              e.preventDefault()
              try {
                const compressed = await compressImage(blob)
                setScreenshot(compressed)
              } catch (_) {
                const file = new File([blob], 'clipboard.png', { type: blob.type })
                setScreenshot(file)
              }
              if (typeof showSnackbar === 'function') showSnackbar('Image pasted from clipboard', 'success')
              return
            }
          }
        }
      } catch (err) {
        // ignore
      }
    }
    window.addEventListener('paste', handler)
    return () => window.removeEventListener('paste', handler)
  }, [showSnackbar, enabled])

  const clear = useCallback(() => setScreenshot(null), [])

  return { screenshot, previewUrl, setScreenshot, onFileChange, clear }
}
