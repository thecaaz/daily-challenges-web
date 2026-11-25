import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import compressImage, { dataUrlToFile } from '../utils/compressImage'

const DATA_URL = 'data:image/png;base64,AAAA'

let imageSize
let encoder
let draws

class FakeImage {
  set src(value) {
    this._src = value
    this.naturalWidth = imageSize.width
    this.naturalHeight = imageSize.height
    queueMicrotask(() => (imageSize.fail ? this.onerror(new Error('decode')) : this.onload()))
  }
  get src() { return this._src }
}

function fakeCanvas() {
  const canvas = {
    width: 0,
    height: 0,
    getContext: () => ({ drawImage: (_img, _x, _y, w, h) => draws.push([w, h]) }),
    toBlob: (callback, format, quality) => callback(encoder({ width: canvas.width, height: canvas.height, format, quality })),
  }
  return canvas
}

const blobOf = (size, type) => {
  const blob = new Blob(['x'], { type })
  Object.defineProperty(blob, 'size', { value: size })
  return blob
}

describe('compressImage', () => {
  let originalImage
  let createElement

  beforeEach(() => {
    imageSize = { width: 800, height: 600 }
    draws = []
    encoder = ({ format }) => blobOf(1000, format)
    originalImage = globalThis.Image
    globalThis.Image = FakeImage
    createElement = document.createElement.bind(document)
    vi.spyOn(document, 'createElement').mockImplementation(tag => (tag === 'canvas' ? fakeCanvas() : createElement(tag)))
  })

  afterEach(() => {
    globalThis.Image = originalImage
    vi.restoreAllMocks()
  })

  it('keeps a small image at its size and encodes it as webp', async () => {
    const file = await compressImage(DATA_URL)

    expect(draws).toEqual([[800, 600]])
    expect(file.name).toBe('capture.webp')
    expect(file.type).toBe('image/webp')
  })

  it('scales a large image down to fit 1920x1080, keeping the aspect ratio', async () => {
    imageSize = { width: 3840, height: 1600 }

    await compressImage(DATA_URL)

    expect(draws).toEqual([[1920, 800]])
  })

  it('honours custom bounds and format', async () => {
    imageSize = { width: 400, height: 400 }

    const file = await compressImage(DATA_URL, { maxWidth: 100, maxHeight: 200, format: 'image/png' })

    expect(draws).toEqual([[100, 100]])
    expect(file.name).toBe('capture.png')
  })

  it('falls back to jpeg when the browser cannot encode webp', async () => {
    encoder = ({ format }) => blobOf(1000, format === 'image/webp' ? 'image/png' : format)

    const file = await compressImage(DATA_URL)

    expect(file.name).toBe('capture.jpg')
    expect(file.type).toBe('image/jpeg')
  })

  it('lowers the quality until the image fits', async () => {
    const qualities = []
    encoder = ({ format, quality }) => {
      qualities.push(quality)
      return blobOf(quality > 0.7 ? 5000 : 100, format)
    }

    await compressImage(DATA_URL, { maxBytes: 1000 })

    expect(qualities).toEqual([0.85, 0.8, 0.75, 0.7])
    expect(draws).toEqual([[800, 600]])
  })

  it('downscales once lowering the quality is not enough', async () => {
    encoder = ({ width, format }) => blobOf(width > 700 ? 5000 : 100, format)

    await compressImage(DATA_URL, { maxBytes: 1000 })

    expect(draws).toEqual([[800, 600], [600, 450]])
  })

  it('gives up after the last downscale step and returns the smallest attempt', async () => {
    encoder = ({ format }) => blobOf(5000, format)

    const file = await compressImage(DATA_URL, { maxBytes: 1000 })

    expect(draws).toEqual([[800, 600], [600, 450], [450, 338], [338, 254], [254, 191]])
    expect(file).toBeInstanceOf(File)
  })

  it('throws when the canvas cannot encode', async () => {
    encoder = () => null

    await expect(compressImage(DATA_URL)).rejects.toThrow('Could not encode image')
  })

  it('rejects when the image cannot be decoded', async () => {
    imageSize = { width: 1, height: 1, fail: true }

    await expect(compressImage(DATA_URL)).rejects.toBeTruthy()
  })

  it('reads a File source as a data URL first', async () => {
    const file = await compressImage(new File(['abc'], 'a.png', { type: 'image/png' }))

    expect(file.name).toBe('capture.webp')
  })
})

describe('dataUrlToFile', () => {
  it('decodes the payload with the mime type from the data URL', async () => {
    const file = dataUrlToFile('data:image/jpeg;base64,' + btoa('hello'), 'x.jpg')

    expect(file.name).toBe('x.jpg')
    expect(file.type).toBe('image/jpeg')
    expect(await file.text()).toBe('hello')
  })

  it('defaults to png without a mime type', () => {
    expect(dataUrlToFile('data:,' + btoa('a'), 'x').type).toBe('image/png')
  })
})
