const MAX_WIDTH = 1920
const MAX_HEIGHT = 1080
const DEFAULT_FORMAT = 'image/webp'
const DEFAULT_QUALITY = 0.85
const QUALITY_STEP = 0.05
const MIN_QUALITY = 0.5
const DEFAULT_MAX_BYTES = 2 * 1024 * 1024 // 2 MB

/**
 * Convert a base64 data-URL to a File object (no re-encoding).
 */
export function dataUrlToFile(dataUrl, filename) {
  const arr = dataUrl.split(',')
  const mimeMatch = arr[0].match(/:(.*?);/)
  const mime = mimeMatch ? mimeMatch[1] : 'image/png'
  const bstr = atob(arr[1])
  let n = bstr.length
  const u8arr = new Uint8Array(n)
  while (n--) u8arr[n] = bstr.charCodeAt(n)
  return new File([u8arr], filename, { type: mime })
}

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve(img)
    img.onerror = reject
    img.src = src
  })
}

function canvasToBlob(canvas, format, quality) {
  return new Promise(resolve => {
    canvas.toBlob(blob => resolve(blob), format, quality)
  })
}

/**
 * Compress / downscale a screenshot.
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
    quality: startQuality = DEFAULT_QUALITY,
    maxBytes = DEFAULT_MAX_BYTES,
  } = opts

  // Resolve source to a data-URL string
  let dataUrl
  if (typeof source === 'string') {
    dataUrl = source
  } else {
    dataUrl = await new Promise((resolve, reject) => {
      const reader = new FileReader()
      reader.onload = () => resolve(reader.result)
      reader.onerror = reject
      reader.readAsDataURL(source)
    })
  }

  const img = await loadImage(dataUrl)

  // Calculate target dimensions (scale down only)
  let { naturalWidth: w, naturalHeight: h } = img
  if (w > maxWidth || h > maxHeight) {
    const ratio = Math.min(maxWidth / w, maxHeight / h)
    w = Math.round(w * ratio)
    h = Math.round(h * ratio)
  }

  const canvas = document.createElement('canvas')
  canvas.width = w
  canvas.height = h
  const ctx = canvas.getContext('2d')
  ctx.drawImage(img, 0, 0, w, h)

  const ext = format === 'image/webp' ? 'webp' : format === 'image/jpeg' ? 'jpg' : 'png'

  // Iteratively reduce quality until under maxBytes
  let quality = startQuality
  let blob = await canvasToBlob(canvas, format, quality)
  while (blob.size > maxBytes && quality > MIN_QUALITY) {
    quality = Math.round((quality - QUALITY_STEP) * 100) / 100
    blob = await canvasToBlob(canvas, format, quality)
  }

  return new File([blob], `capture.${ext}`, { type: format })
}
