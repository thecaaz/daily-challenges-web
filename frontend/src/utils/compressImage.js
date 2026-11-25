const MAX_WIDTH = 1920
const MAX_HEIGHT = 1080
const DEFAULT_FORMAT = 'image/webp'
const DEFAULT_QUALITY = 0.85
const QUALITY_STEP = 0.05
const MIN_QUALITY = 0.5
const DEFAULT_MAX_BYTES = 2 * 1024 * 1024
const DOWNSCALE_FACTOR = 0.75
const MAX_DOWNSCALE_STEPS = 4
const HUNDREDTHS = 100
const MIN_DIMENSION = 1

const JPEG = 'image/jpeg'
const PNG = 'image/png'
const WEBP = 'image/webp'
const DATA_URL_MIME = /:(.*?);/

function extensionFor(type) {
  if (type === WEBP) return 'webp'
  if (type === JPEG) return 'jpg'
  return 'png'
}

/**
 * Convert a base64 data-URL to a File object (no re-encoding).
 */
export function dataUrlToFile(dataUrl, filename) {
  const [header, payload] = dataUrl.split(',')
  const mimeMatch = header.match(DATA_URL_MIME)
  const mime = mimeMatch ? mimeMatch[1] : PNG
  const bytes = Uint8Array.from(atob(payload), char => char.charCodeAt(0))
  return new File([bytes], filename, { type: mime })
}

/**
 * Compress / downscale a screenshot: scale it down to fit the bounds, then
 * lower the quality and, if that is not enough, the dimensions until it fits
 * under `maxBytes` (or the last downscale step is reached).
 *
 * @param {string|File|Blob} source – data-URL string, File, or Blob
 * @param {object}  [opts]
 * @param {number}  [opts.maxWidth=1920]
 * @param {number}  [opts.maxHeight=1080]
 * @param {string}  [opts.format='image/webp']
 * @param {number}  [opts.quality=0.85]
 * @param {number}  [opts.maxBytes=2097152]
 * @returns {Promise<File>}
 */
export default async function compressImage(source, opts = {}) {
  const {
    maxWidth = MAX_WIDTH,
    maxHeight = MAX_HEIGHT,
    format = DEFAULT_FORMAT,
    quality = DEFAULT_QUALITY,
    maxBytes = DEFAULT_MAX_BYTES,
  } = opts

  const image = await loadImage(await toDataUrl(source))
  const size = fitWithin({ width: image.naturalWidth, height: image.naturalHeight }, { maxWidth, maxHeight })
  const encoder = createCanvasEncoder(image, format)

  const blob = await encodeWithinBytes(encoder, size, { startQuality: quality, maxBytes })
  if (!blob) throw new Error('Could not encode image')

  const type = blob.type || encoder.format()
  return new File([blob], `capture.${extensionFor(type)}`, { type })
}

function toDataUrl(source) {
  if (typeof source === 'string') return Promise.resolve(source)
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result)
    reader.onerror = reject
    reader.readAsDataURL(source)
  })
}

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve(img)
    img.onerror = reject
    img.src = src
  })
}

/** Scales down only, keeping the aspect ratio. */
function fitWithin({ width, height }, { maxWidth, maxHeight }) {
  if (width <= maxWidth && height <= maxHeight) return { width, height }
  const ratio = Math.min(maxWidth / width, maxHeight / height)
  return { width: Math.round(width * ratio), height: Math.round(height * ratio) }
}

function downscale({ width, height }) {
  return {
    width: Math.max(MIN_DIMENSION, Math.round(width * DOWNSCALE_FACTOR)),
    height: Math.max(MIN_DIMENSION, Math.round(height * DOWNSCALE_FACTOR)),
  }
}

/**
 * Draws the image on a canvas and encodes it. Browsers that can't encode the
 * requested format (e.g. Safari and webp) silently return PNG; the encoder
 * then switches to JPEG for good, so the quality setting still has an effect.
 */
function createCanvasEncoder(image, requestedFormat) {
  const canvas = document.createElement('canvas')
  const context = canvas.getContext('2d')
  let format = requestedFormat

  const toBlob = quality => new Promise(resolve => canvas.toBlob(resolve, format, quality))
  const wasFormatIgnored = blob =>
    blob && format !== JPEG && format !== PNG && blob.type !== format

  return {
    format: () => format,

    draw({ width, height }) {
      canvas.width = width
      canvas.height = height
      context.drawImage(image, 0, 0, width, height)
    },

    async encode(quality) {
      const blob = await toBlob(quality)
      if (!wasFormatIgnored(blob)) return blob
      format = JPEG
      return toBlob(quality)
    },
  }
}

async function encodeWithinBytes(encoder, size, { startQuality, maxBytes }) {
  const isDone = blob => !blob || blob.size <= maxBytes
  let currentSize = size
  encoder.draw(currentSize)
  let blob = await encoder.encode(startQuality)

  for (let step = 0; !isDone(blob); step++) {
    blob = await lowerQualityUntilFits(encoder, blob, { startQuality, maxBytes })
    if (isDone(blob) || step === MAX_DOWNSCALE_STEPS) break
    currentSize = downscale(currentSize)
    encoder.draw(currentSize)
    blob = await encoder.encode(startQuality)
  }
  return blob
}

async function lowerQualityUntilFits(encoder, blob, { startQuality, maxBytes }) {
  let quality = startQuality
  let current = blob
  while (current && current.size > maxBytes && quality > MIN_QUALITY) {
    quality = Math.round((quality - QUALITY_STEP) * HUNDREDTHS) / HUNDREDTHS
    current = await encoder.encode(quality)
  }
  return current
}
