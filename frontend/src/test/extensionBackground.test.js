import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import loadExtensionScript from './loadExtensionScript'

const APP_URL = 'https://challenges.caaz.dev/play/1'
const DATA_URL = 'data:image/png;base64,AAAA'

let onMessage
let tabs

function appSender(overrides = {}) {
  return {
    frameId: 0,
    url: APP_URL,
    tab: { id: 7, windowId: 3, active: true, url: APP_URL },
    ...overrides,
  }
}

function loadBackground() {
  tabs = {
    captureTab: vi.fn(async () => DATA_URL),
    captureVisibleTab: vi.fn(async () => DATA_URL),
  }
  globalThis.browser = {
    runtime: { onMessage: { addListener: listener => { onMessage = listener } } },
    tabs,
  }
  loadExtensionScript('app-origins.js')
  loadExtensionScript('background.js')
}

describe('extension background CAPTURE_TAB', () => {
  beforeEach(loadBackground)

  afterEach(() => {
    delete globalThis.browser
  })

  it('ignores other messages', () => {
    expect(onMessage({ type: 'OTHER' }, appSender())).toBeUndefined()
    expect(onMessage(null, appSender())).toBeUndefined()
  })

  it('captures the rounded, clamped rect of the requesting tab as png', async () => {
    const rect = { x: -4.4, y: 10.6, width: 300.4, height: '200' }

    const response = await onMessage({ type: 'CAPTURE_TAB', rect }, appSender())

    expect(tabs.captureTab).toHaveBeenCalledWith(7, { format: 'png', rect: { x: 0, y: 11, width: 300, height: 200 } })
    expect(response).toEqual({ ok: true, dataUrl: DATA_URL, cropped: true })
  })

  it('captures the visible viewport without a usable rect', async () => {
    const response = await onMessage({ type: 'CAPTURE_TAB', rect: { x: 0, y: 0, width: 0.5, height: 10 } }, appSender())

    expect(tabs.captureTab).toHaveBeenCalledWith(7, { format: 'png' })
    expect(response).toEqual({ ok: true, dataUrl: DATA_URL, cropped: false })
  })

  it.each([
    ['missing', undefined],
    ['non-numeric', { x: 'a', y: 0, width: 10, height: 10 }],
    ['oversized', { x: 0, y: 0, width: 8193, height: 10 }],
  ])('treats a %s rect as no rect', async (_, rect) => {
    await onMessage({ type: 'CAPTURE_TAB', rect }, appSender())

    expect(tabs.captureTab).toHaveBeenCalledWith(7, { format: 'png' })
  })

  it('accepts a rect at the maximum side length', async () => {
    const rect = { x: 0, y: 0, width: 8192, height: 8192 }

    await onMessage({ type: 'CAPTURE_TAB', rect }, appSender())

    expect(tabs.captureTab).toHaveBeenCalledWith(7, { format: 'png', rect })
  })

  it('falls back to the visible tab of the window without tabs.captureTab', async () => {
    tabs.captureTab = undefined

    const response = await onMessage({ type: 'CAPTURE_TAB', rect: { x: 0, y: 0, width: 10, height: 10 } }, appSender())

    expect(tabs.captureVisibleTab).toHaveBeenCalledWith(3, { format: 'png' })
    expect(response).toEqual({ ok: true, dataUrl: DATA_URL, cropped: false })
  })

  it.each([
    ['no sender tab', { tab: undefined }],
    ['no sender tab', { tab: { id: 'x', active: true, url: APP_URL } }],
    ['capture must be requested by the top frame', { frameId: 2 }],
    ['tab is not active', { tab: { id: 7, active: false, url: APP_URL } }],
    ['origin not allowed', { tab: { id: 7, active: true, url: 'https://evil.example/' } }],
    ['origin not allowed', { url: 'https://evil.example/' }],
  ])('refuses with "%s"', async (error, overrides) => {
    const response = await onMessage({ type: 'CAPTURE_TAB' }, appSender(overrides))

    expect(response).toEqual({ ok: false, error })
    expect(tabs.captureTab).not.toHaveBeenCalled()
  })

  it('reports an empty capture as "no data"', async () => {
    tabs.captureTab.mockResolvedValueOnce('')

    expect(await onMessage({ type: 'CAPTURE_TAB' }, appSender())).toEqual({ ok: false, error: 'no data' })
  })

  it('reports a failed capture with its message', async () => {
    tabs.captureTab.mockRejectedValueOnce(new Error('boom'))

    expect(await onMessage({ type: 'CAPTURE_TAB' }, appSender())).toEqual({ ok: false, error: 'boom' })
  })

  it('reports a non-Error rejection as text', async () => {
    tabs.captureTab.mockRejectedValueOnce('denied')

    expect(await onMessage({ type: 'CAPTURE_TAB' }, appSender())).toEqual({ ok: false, error: 'denied' })
  })
})
