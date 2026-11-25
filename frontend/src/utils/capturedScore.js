import compressImage from './compressImage'

const MAX_SCORE_LENGTH = 64
const EXPONENT_NOTATION = /e/i
const PLAIN_NUMBER_FORMAT = { useGrouping: false, maximumFractionDigits: 20 }
const IMAGE_DATA_URL_PREFIX = 'data:image/'

export const CaptureError = Object.freeze({
  NO_RESPONSE: 'the extension did not respond',
  NO_IMAGE: 'capture returned no image',
  UNREADABLE_IMAGE: 'could not process the screenshot',
  REQUEST_FAILED: 'could not request a capture',
})

/** Score from the extension -> form value. Anything not a usable score -> ''. */
export function normalizeCapturedScore(value) {
  if (typeof value === 'number') return formatCapturedNumber(value)
  if (typeof value === 'string') return trimCapturedText(value)
  return ''
}

function formatCapturedNumber(value) {
  if (!Number.isFinite(value)) return ''
  const text = String(value)
  return EXPONENT_NOTATION.test(text) ? value.toLocaleString('en-US', PLAIN_NUMBER_FORMAT) : text
}

function trimCapturedText(value) {
  const text = value.trim()
  return text.length > 0 && text.length <= MAX_SCORE_LENGTH ? text : ''
}

/**
 * Turns the extension's CAPTURE_RESPONSE into `{ file }` or `{ error }`.
 * `onScreenshotTaken` runs before the (slow) compression, so the caller can
 * take its overlay down early.
 */
export async function readCapturedScreenshot(response, onScreenshotTaken) {
  if (response.error) return { error: String(response.error) }
  if (!isImageDataUrl(response.dataUrl)) return { error: CaptureError.NO_IMAGE }
  try {
    onScreenshotTaken()
    return { file: await compressImage(response.dataUrl) }
  } catch {
    return { error: CaptureError.UNREADABLE_IMAGE }
  }
}

function isImageDataUrl(value) {
  return typeof value === 'string' && value.startsWith(IMAGE_DATA_URL_PREFIX)
}
