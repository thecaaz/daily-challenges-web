/**
 * window.postMessage protocol of the ScoreBridge browser extension.
 *
 * The extension's content script runs in this window and answers from it;
 * game frames answer from their own window and origin. Every request carries
 * a nonce so a reply can be matched to exactly one request.
 */

export const MessageType = Object.freeze({
  GET_ADAPTERS: 'GET_ADAPTERS',
  ADAPTERS_RESPONSE: 'ADAPTERS_RESPONSE',
  HAS_ADAPTER: 'HAS_ADAPTER',
  HAS_ADAPTER_RESPONSE: 'HAS_ADAPTER_RESPONSE',
  GET_SCORE: 'GET_SCORE',
  SCORE_RESPONSE: 'SCORE_RESPONSE',
  ARM_CAPTURE: 'SCOREBRIDGE_ARM',
  REQUEST_VISIBLE_TAB: 'REQUEST_VISIBLE_TAB_FROM_PARENT',
  CAPTURE_RESPONSE: 'CAPTURE_RESPONSE',
})

const GAME_FRAME_SENDER = 'ScoreBridgeParent'
const OPAQUE_ORIGIN = 'null'
const RADIX_36 = 36

export function makeNonce() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  return `${Date.now().toString(RADIX_36)}-${randomBase36()}${randomBase36()}`
}

function randomBase36() {
  return Math.random().toString(RADIX_36).slice(2)
}

/** The origin of `url`, or null when it has none (invalid or opaque). */
export function originOf(url) {
  try {
    const origin = new URL(url).origin
    return origin && origin !== OPAQUE_ORIGIN ? origin : null
  } catch {
    return null
  }
}

export function isFromContentScript(event) {
  return event.source === window && event.origin === window.location.origin
}

export function postToContentScript(message) {
  window.postMessage(message, window.location.origin)
}

/**
 * @param {{ gameWindow: Window, origin: string }} gameFrame
 * @param {object} message - `type` and `nonce`
 */
export function postToGameFrame({ gameWindow, origin }, message) {
  gameWindow.postMessage({ source: GAME_FRAME_SENDER, ...message }, origin)
}
