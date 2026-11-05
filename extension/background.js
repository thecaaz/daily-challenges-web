// ScoreBridge background script: captures the requesting app tab.
//
// Only the top-level content script of a DailyChallenges app tab may ask for
// a capture, and only while that tab is the active tab of its window. The
// image is cropped to the requested rect by Firefox itself (tabs.captureTab),
// so the full tab image never leaves the extension.
'use strict'

const { isAppOrigin, originOf } = globalThis.ScoreBridgeOrigins

// Upper bound for one capture side, in CSS pixels. Larger rects are refused
// rather than rendered (drawSnapshot allocates the whole bitmap).
const MAX_RECT_SIDE = 8192

function sanitizeRect(rect) {
  if (!rect || typeof rect !== 'object') return null
  const x = Number(rect.x)
  const y = Number(rect.y)
  const width = Number(rect.width)
  const height = Number(rect.height)
  if (![x, y, width, height].every(Number.isFinite)) return null
  if (width < 1 || height < 1 || width > MAX_RECT_SIDE || height > MAX_RECT_SIDE) return null
  return {
    x: Math.max(0, Math.round(x)),
    y: Math.max(0, Math.round(y)),
    width: Math.round(width),
    height: Math.round(height)
  }
}

function refuse(error) {
  return Promise.resolve({ ok: false, error })
}

async function capture(sender, rect) {
  const tab = sender.tab
  const tabs = browser.tabs
  if (typeof tabs.captureTab === 'function') {
    // Firefox 59+; `rect` is honoured from Firefox 82. Without a rect it
    // captures the tab's visible viewport.
    const options = rect ? { format: 'png', rect } : { format: 'png' }
    const dataUrl = await tabs.captureTab(tab.id, options)
    return { dataUrl, cropped: !!rect }
  }
  // Fallback for very old Firefox: the requesting tab was verified active
  // above, so the visible tab of its window is the requesting tab.
  const dataUrl = await tabs.captureVisibleTab(tab.windowId, { format: 'png' })
  return { dataUrl, cropped: false }
}

browser.runtime.onMessage.addListener((msg, sender) => {
  if (!msg || msg.type !== 'CAPTURE_TAB') return undefined

  const tab = sender && sender.tab
  if (!tab || typeof tab.id !== 'number') return refuse('no sender tab')
  if (sender.frameId !== 0) return refuse('capture must be requested by the top frame')
  if (!tab.active) return refuse('tab is not active')
  if (!isAppOrigin(originOf(tab.url)) || !isAppOrigin(originOf(sender.url))) {
    return refuse('origin not allowed')
  }

  const rect = sanitizeRect(msg.rect)
  return capture(sender, rect).then(
    ({ dataUrl, cropped }) => (dataUrl ? { ok: true, dataUrl, cropped } : { ok: false, error: 'no data' }),
    err => ({ ok: false, error: String((err && err.message) || err) })
  )
})
