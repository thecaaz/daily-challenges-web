// ScoreBridge background script: captures the requesting app tab.
//
// Only the top-level content script of a DailyChallenges app tab may ask for
// a capture, and only while that tab is the active tab of its window. The
// image is cropped to the requested rect by Firefox itself (tabs.captureTab),
// so the full tab image never leaves the extension.
'use strict'

const { isAppOrigin, originOf } = globalThis.ScoreBridgeOrigins

const CAPTURE_TAB = 'CAPTURE_TAB'
const TOP_FRAME_ID = 0
const IMAGE_FORMAT = 'png'
const MIN_RECT_SIDE = 1
// Upper bound for one capture side, in CSS pixels. Larger rects are refused
// rather than rendered (drawSnapshot allocates the whole bitmap).
const MAX_RECT_SIDE = 8192

const CaptureError = Object.freeze({
  NO_SENDER_TAB: 'no sender tab',
  NOT_TOP_FRAME: 'capture must be requested by the top frame',
  TAB_NOT_ACTIVE: 'tab is not active',
  ORIGIN_NOT_ALLOWED: 'origin not allowed',
  NO_DATA: 'no data',
})

browser.runtime.onMessage.addListener((msg, sender) => {
  if (!msg || msg.type !== CAPTURE_TAB) return undefined

  const refusal = refusalFor(sender)
  if (refusal) return Promise.resolve({ ok: false, error: refusal })

  return capture(sender.tab, sanitizeRect(msg.rect)).then(captureResult, captureFailure)
})

/** Why `sender` may not capture, or null when it may. */
function refusalFor(sender) {
  const tab = sender && sender.tab
  if (!tab || typeof tab.id !== 'number') return CaptureError.NO_SENDER_TAB
  if (sender.frameId !== TOP_FRAME_ID) return CaptureError.NOT_TOP_FRAME
  if (!tab.active) return CaptureError.TAB_NOT_ACTIVE
  if (!isAppOrigin(originOf(tab.url)) || !isAppOrigin(originOf(sender.url))) return CaptureError.ORIGIN_NOT_ALLOWED
  return null
}

/** The rect rounded to whole pixels inside the page, or null when it is unusable. */
function sanitizeRect(rect) {
  if (!rect || typeof rect !== 'object') return null
  const x = Number(rect.x)
  const y = Number(rect.y)
  const width = Number(rect.width)
  const height = Number(rect.height)
  if (![x, y, width, height].every(Number.isFinite)) return null
  if (!isCapturableSide(width) || !isCapturableSide(height)) return null
  return {
    x: Math.max(0, Math.round(x)),
    y: Math.max(0, Math.round(y)),
    width: Math.round(width),
    height: Math.round(height)
  }
}

function isCapturableSide(length) {
  return length >= MIN_RECT_SIDE && length <= MAX_RECT_SIDE
}

async function capture(tab, rect) {
  return typeof browser.tabs.captureTab === 'function'
    ? captureTabArea(tab, rect)
    : captureVisibleTabOfWindow(tab)
}

/**
 * tabs.captureTab needs Firefox 59+ and honours `rect` from Firefox 82.
 * Without a rect it captures the tab's visible viewport.
 */
async function captureTabArea(tab, rect) {
  const options = rect ? { format: IMAGE_FORMAT, rect } : { format: IMAGE_FORMAT }
  const dataUrl = await browser.tabs.captureTab(tab.id, options)
  return { dataUrl, cropped: !!rect }
}

/**
 * Fallback for very old Firefox: the requesting tab was verified active, so
 * the visible tab of its window is the requesting tab.
 */
async function captureVisibleTabOfWindow(tab) {
  const dataUrl = await browser.tabs.captureVisibleTab(tab.windowId, { format: IMAGE_FORMAT })
  return { dataUrl, cropped: false }
}

function captureResult({ dataUrl, cropped }) {
  return dataUrl ? { ok: true, dataUrl, cropped } : { ok: false, error: CaptureError.NO_DATA }
}

function captureFailure(err) {
  return { ok: false, error: String((err && err.message) || err) }
}
