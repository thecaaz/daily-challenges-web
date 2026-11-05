import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { useScoreCapture, CAPTURE_TIMEOUT_MS, normalizeCapturedScore } from '../hooks/useScoreCapture'
import compressImage from '../utils/compressImage'
import useAdapter from '../hooks/useAdapter'

vi.mock('../utils/compressImage', () => ({ default: vi.fn() }))
vi.mock('../hooks/useAdapter', () => ({ default: vi.fn() }))

const GAME_URL = 'https://game.example/daily'
const GAME_ORIGIN = 'https://game.example'
const DATA_URL = 'data:image/png;base64,AAAA'

function setup({ isMaximized = false, url = GAME_URL } = {}) {
  const container = document.createElement('div')
  container.style.position = 'relative'
  container.style.width = '640px'
  document.body.appendChild(container)
  const gameWindow = { postMessage: vi.fn() }
  const iframe = {
    src: GAME_URL,
    contentWindow: gameWindow,
    parentElement: container,
    scrollIntoView: vi.fn(),
  }
  const iframeRef = { current: iframe }
  const hook = renderHook(() => useScoreCapture(iframeRef, { isMaximized, url }))
  return { hook, container, gameWindow, iframeRef }
}

function dispatch(data, { source, origin }) {
  window.dispatchEvent(new MessageEvent('message', { data, source, origin }))
}

// Start a capture and let the overlay-settle delay pass so the ARM + capture
// request are posted. Returns the nonces the hook used.
async function startCapture(ctx) {
  const postSpy = vi.spyOn(window, 'postMessage').mockImplementation(() => {})
  await act(async () => {
    ctx.hook.result.current.submitScore()
    await vi.advanceTimersByTimeAsync(300)
  })
  const scoreReq = ctx.gameWindow.postMessage.mock.calls.find(([m]) => m.type === 'GET_SCORE')
  const captureReq = ctx.gameWindow.postMessage.mock.calls.find(([m]) => m.type === 'REQUEST_VISIBLE_TAB_FROM_PARENT')
  const arm = postSpy.mock.calls.find(([m]) => m.type === 'SCOREBRIDGE_ARM')
  return { scoreNonce: scoreReq?.[0].nonce, captureNonce: captureReq?.[0].nonce, scoreReq, captureReq, arm }
}

async function sendScore(ctx, data, opts = {}) {
  await act(async () => {
    dispatch({ type: 'SCORE_RESPONSE', ...data }, { source: ctx.gameWindow, origin: GAME_ORIGIN, ...opts })
  })
}

async function sendCapture(data, opts = {}) {
  await act(async () => {
    dispatch({ type: 'CAPTURE_RESPONSE', ...data }, { source: window, origin: window.location.origin, ...opts })
    await Promise.resolve()
  })
}

describe('useScoreCapture', () => {
  let ctx

  beforeEach(() => {
    vi.useFakeTimers()
    compressImage.mockReset()
    compressImage.mockResolvedValue(new File(['x'], 'capture.webp', { type: 'image/webp' }))
    useAdapter.mockReturnValue({ name: 'Game' })
  })

  afterEach(() => {
    ctx?.hook.unmount()
    ctx?.container.remove()
    ctx = null
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('keeps the original return fields and exposes canCapture', () => {
    ctx = setup()
    const r = ctx.hook.result.current
    for (const key of ['score', 'setScore', 'capturedFile', 'setCapturedFile', 'showSubmission', 'setShowSubmission', 'submitScore']) {
      expect(r).toHaveProperty(key)
    }
    expect(r.canCapture).toBe(true)
    expect(useAdapter).toHaveBeenCalledWith(GAME_URL)
  })

  it('canCapture is false when no adapter exists', () => {
    useAdapter.mockReturnValue(null)
    ctx = setup()
    expect(ctx.hook.result.current.canCapture).toBe(false)
  })

  it('posts requests to the game origin and arms the capture on its own origin', async () => {
    ctx = setup()
    const { scoreReq, captureReq, arm, captureNonce } = await startCapture(ctx)
    expect(scoreReq[1]).toBe(GAME_ORIGIN)
    expect(captureReq[1]).toBe(GAME_ORIGIN)
    expect(arm).toEqual([{ type: 'SCOREBRIDGE_ARM', nonce: captureNonce }, window.location.origin])
  })

  describe('SCORE_RESPONSE', () => {
    it('accepts the score with the right nonce, source and origin, once', async () => {
      ctx = setup()
      const { scoreNonce } = await startCapture(ctx)
      await sendScore(ctx, { nonce: scoreNonce, score: 40456 })
      expect(ctx.hook.result.current.score).toBe('40456')

      // Nonce is single-use.
      await sendScore(ctx, { nonce: scoreNonce, score: 1 })
      expect(ctx.hook.result.current.score).toBe('40456')
    })

    it('rejects a response without a nonce', async () => {
      ctx = setup()
      await startCapture(ctx)
      await sendScore(ctx, { score: 99999 })
      expect(ctx.hook.result.current.score).toBe('')
    })

    it('rejects a response with the wrong nonce', async () => {
      ctx = setup()
      await startCapture(ctx)
      await sendScore(ctx, { nonce: 'not-the-nonce', score: 99999 })
      expect(ctx.hook.result.current.score).toBe('')
    })

    it('rejects a response before any request was made', async () => {
      ctx = setup()
      await sendScore(ctx, { nonce: undefined, score: 99999 })
      expect(ctx.hook.result.current.score).toBe('')
    })

    it('rejects a response from the wrong source', async () => {
      ctx = setup()
      const { scoreNonce } = await startCapture(ctx)
      await sendScore(ctx, { nonce: scoreNonce, score: 99999 }, { source: window })
      expect(ctx.hook.result.current.score).toBe('')
    })

    it('rejects a response from the wrong origin', async () => {
      ctx = setup()
      const { scoreNonce } = await startCapture(ctx)
      await sendScore(ctx, { nonce: scoreNonce, score: 99999 }, { origin: 'https://evil.example' })
      expect(ctx.hook.result.current.score).toBe('')
    })

    it('accepts a late score after the iframe unmounted (the form replaced it)', async () => {
      ctx = setup()
      const { scoreNonce, captureNonce } = await startCapture(ctx)
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      expect(ctx.hook.result.current.showSubmission).toBe(true)

      // The page swaps the iframe for the submission form.
      ctx.iframeRef.current = null
      await sendScore(ctx, { nonce: scoreNonce, score: 777 })
      expect(ctx.hook.result.current.score).toBe('777')
    })

    it('validates against the window/origin snapshotted when the capture started', async () => {
      ctx = setup()
      const { scoreNonce } = await startCapture(ctx)
      // The iframe now points somewhere else: the response must still come from the original game window.
      const otherWindow = { postMessage: vi.fn() }
      ctx.iframeRef.current = { ...ctx.iframeRef.current, src: 'https://evil.example/', contentWindow: otherWindow }
      await sendScore(ctx, { nonce: scoreNonce, score: 1 }, { source: otherWindow, origin: 'https://evil.example' })
      expect(ctx.hook.result.current.score).toBe('')
      await sendScore(ctx, { nonce: scoreNonce, score: 2 })
      expect(ctx.hook.result.current.score).toBe('2')
    })

    it('turns a null score into "" and still shows the form with the screenshot', async () => {
      ctx = setup()
      const { scoreNonce, captureNonce } = await startCapture(ctx)
      await sendScore(ctx, { nonce: scoreNonce, score: null })
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      const r = ctx.hook.result.current
      expect(r.score).toBe('')
      expect(r.capturedFile).toBeInstanceOf(File)
      expect(r.showSubmission).toBe(true)
    })
  })

  describe('CAPTURE_RESPONSE', () => {
    it('accepts the capture from this window with the armed nonce and restores the overlay', async () => {
      ctx = setup()
      const { captureNonce } = await startCapture(ctx)
      expect(ctx.container.style.position).toBe('fixed')

      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      expect(compressImage).toHaveBeenCalledWith(DATA_URL)
      expect(ctx.hook.result.current.capturedFile).toBeInstanceOf(File)
      expect(ctx.hook.result.current.showSubmission).toBe(true)
      expect(ctx.container.style.position).toBe('relative')
    })

    it('rejects a capture from another source (e.g. the game iframe)', async () => {
      ctx = setup()
      const { captureNonce } = await startCapture(ctx)
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL }, { source: ctx.gameWindow, origin: GAME_ORIGIN })
      expect(compressImage).not.toHaveBeenCalled()
      expect(ctx.hook.result.current.capturedFile).toBeNull()
    })

    it('rejects a capture without or with the wrong nonce', async () => {
      ctx = setup()
      await startCapture(ctx)
      await sendCapture({ dataUrl: DATA_URL })
      await sendCapture({ nonce: 'wrong', dataUrl: DATA_URL })
      expect(compressImage).not.toHaveBeenCalled()
      expect(ctx.hook.result.current.showSubmission).toBe(false)
    })

    it('restores the overlay when compressImage fails', async () => {
      ctx = setup()
      compressImage.mockRejectedValue(new Error('decode failed'))
      const { captureNonce } = await startCapture(ctx)
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      expect(ctx.container.style.position).toBe('relative')
      expect(ctx.hook.result.current.captureError).toBeTruthy()
    })

    it('restores the overlay on an extension error', async () => {
      ctx = setup()
      const { captureNonce } = await startCapture(ctx)
      await sendCapture({ nonce: captureNonce, error: 'origin not allowed' })
      expect(ctx.container.style.position).toBe('relative')
      expect(ctx.hook.result.current.captureError).toBe('origin not allowed')
    })
  })

  describe('cancelCapture', () => {
    it('restores the overlay, stops capturing and ignores late responses', async () => {
      ctx = setup()
      const { scoreNonce, captureNonce } = await startCapture(ctx)
      expect(ctx.hook.result.current.capturing).toBe(true)
      expect(ctx.container.style.position).toBe('fixed')

      act(() => { ctx.hook.result.current.cancelCapture() })
      expect(ctx.hook.result.current.capturing).toBe(false)
      expect(ctx.container.style.position).toBe('relative')

      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      await sendScore(ctx, { nonce: scoreNonce, score: 123 })
      const r = ctx.hook.result.current
      expect(compressImage).not.toHaveBeenCalled()
      expect(r.score).toBe('')
      expect(r.capturedFile).toBeNull()
      expect(r.showSubmission).toBe(false)
      expect(r.captureError).toBeNull()

      // The timeout of the cancelled capture doesn't open the form later either.
      await act(async () => { await vi.advanceTimersByTimeAsync(CAPTURE_TIMEOUT_MS) })
      expect(ctx.hook.result.current.showSubmission).toBe(false)
      expect(ctx.hook.result.current.captureError).toBeNull()
    })

    it('drops a screenshot that finishes compressing after the cancel', async () => {
      ctx = setup()
      let resolveCompress
      compressImage.mockImplementation(() => new Promise(r => { resolveCompress = r }))
      const { captureNonce } = await startCapture(ctx)
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      expect(compressImage).toHaveBeenCalled()

      act(() => { ctx.hook.result.current.cancelCapture() })
      await act(async () => { resolveCompress(new File(['x'], 'late.webp', { type: 'image/webp' })) })
      expect(ctx.hook.result.current.capturedFile).toBeNull()
      expect(ctx.hook.result.current.showSubmission).toBe(false)
    })

    it('allows a new capture after a cancel', async () => {
      ctx = setup()
      await startCapture(ctx)
      act(() => { ctx.hook.result.current.cancelCapture() })
      ctx.gameWindow.postMessage.mockClear()
      const { captureNonce } = await startCapture(ctx)
      expect(captureNonce).toBeTruthy()
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      expect(ctx.hook.result.current.capturedFile).toBeInstanceOf(File)
      expect(ctx.hook.result.current.showSubmission).toBe(true)
    })
  })

  describe('overlay', () => {
    it('restores the overlay after the timeout when the extension never answers', async () => {
      ctx = setup()
      const { captureNonce } = await startCapture(ctx)
      expect(ctx.container.style.position).toBe('fixed')
      expect(ctx.container.style.zIndex).toBe('2147483647')

      await act(async () => { await vi.advanceTimersByTimeAsync(CAPTURE_TIMEOUT_MS) })
      expect(ctx.container.style.position).toBe('relative')
      expect(ctx.container.style.width).toBe('640px')
      expect(ctx.hook.result.current.capturing).toBe(false)
      expect(ctx.hook.result.current.captureError).toBeTruthy()

      // A late answer is ignored.
      await sendCapture({ nonce: captureNonce, dataUrl: DATA_URL })
      expect(compressImage).not.toHaveBeenCalled()
    })

    it('restores the overlay on unmount', async () => {
      ctx = setup()
      await startCapture(ctx)
      expect(ctx.container.style.position).toBe('fixed')
      ctx.hook.unmount()
      expect(ctx.container.style.position).toBe('relative')
      ctx.hook = { unmount() {} }
    })

    it('does not apply the overlay when the player is maximized', async () => {
      ctx = setup({ isMaximized: true })
      await startCapture(ctx)
      expect(ctx.container.style.position).toBe('relative')
    })
  })
})

describe('normalizeCapturedScore', () => {
  it('maps unusable values to ""', () => {
    expect(normalizeCapturedScore(null)).toBe('')
    expect(normalizeCapturedScore(undefined)).toBe('')
    expect(normalizeCapturedScore(NaN)).toBe('')
    expect(normalizeCapturedScore(Infinity)).toBe('')
    expect(normalizeCapturedScore({})).toBe('')
  })
  it('keeps numbers without exponent notation', () => {
    expect(normalizeCapturedScore(437.25)).toBe('437.25')
    expect(normalizeCapturedScore(0)).toBe('0')
    expect(normalizeCapturedScore(1e21)).toBe('1000000000000000000000')
  })
})
