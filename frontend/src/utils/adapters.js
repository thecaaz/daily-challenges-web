// Adapter helper: asks the ScoreBridge browser extension for its adapters via
// window.postMessage (the extension's content script runs in this window).
//
// - Concurrent fetchAdapters() calls share one in-flight request.
// - When the extension doesn't answer GET_ADAPTERS, it is assumed absent and
//   every lookup returns "no adapter" immediately, without another timeout.
//   That assumption is re-checked after NO_EXTENSION_RECHECK_MS, or at once
//   via refreshAdapters() (e.g. on the extension install page).
// - The very first miss is re-checked once after EARLY_RECHECK_MS: on a hard
//   load the content script is injected at document_idle and may not be
//   listening yet when the app first asks.
// - subscribeAdapters(listener) is called whenever the answer changes (e.g.
//   the extension showed up), so hooks can look their adapter up again.

const DEFAULT_TIMEOUT_MS = 800
const HAS_ADAPTER_TIMEOUT_MS = 300
export const NO_EXTENSION_RECHECK_MS = 30_000
export const EARLY_RECHECK_MS = 1500

let cachedAdapters = null // array once the extension has answered
let noExtensionUntil = 0 // while Date.now() < this, assume no extension
let inflight = null
let earlyRecheckDone = false
let earlyRecheckTimer = null
const listeners = new Set()

/** Call `listener` whenever the known adapters change. Returns an unsubscribe function. */
export function subscribeAdapters(listener) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

function notifyListeners() {
  for (const listener of Array.from(listeners)) {
    try { listener(cachedAdapters || []) } catch (e) { /* a listener's bug must not break the others */ }
  }
}

function scheduleEarlyRecheck(timeoutMs) {
  if (earlyRecheckDone || typeof setTimeout !== 'function') return
  earlyRecheckDone = true
  earlyRecheckTimer = setTimeout(() => {
    earlyRecheckTimer = null
    // Only if nothing else has found the extension in the meantime.
    if (cachedAdapters === null) fetchAdapters({ timeoutMs, force: true })
  }, EARLY_RECHECK_MS)
}

function hostnameFromUrl(urlOrHost) {
  if (!urlOrHost) return ''
  try {
    const u = new URL(urlOrHost)
    return u.hostname || ''
  } catch (e) {
    return String(urlOrHost)
  }
}

function matchDescriptorMatch(host, descriptor) {
  if (!descriptor || !host) return false
  const val = String(descriptor.value || '').toLowerCase()
  const h = host.toLowerCase()
  switch (descriptor.type) {
    case 'includes':
      return h.includes(val)
    case 'equals':
      return h === val
    case 'regex':
      try { return new RegExp(descriptor.value).test(host) } catch (e) { return false }
    default:
      return false
  }
}

function makeNonce() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

function requestFromExtension(message, expectedResponseType, timeoutMs = DEFAULT_TIMEOUT_MS) {
  return new Promise(resolve => {
    if (typeof window === 'undefined' || typeof window.postMessage !== 'function') {
      return resolve(null)
    }

    const nonce = makeNonce()
    let timer = null
    const handler = (ev) => {
      try {
        // The content script answers from this same window.
        if (ev.source !== window || ev.origin !== window.location.origin) return
        if (!ev.data || ev.data.type !== expectedResponseType || ev.data.nonce !== nonce) return
        window.removeEventListener('message', handler)
        clearTimeout(timer)
        resolve(ev.data)
      } catch (e) {
        // ignore
      }
    }

    window.addEventListener('message', handler)

    try {
      window.postMessage(Object.assign({}, message, { nonce }), window.location.origin)
    } catch (e) {
      window.removeEventListener('message', handler)
      return resolve(null)
    }

    timer = setTimeout(() => {
      window.removeEventListener('message', handler)
      resolve(null)
    }, timeoutMs)
  })
}

/**
 * Whether the extension answered: true (answered), false (no answer recently),
 * null (not asked yet, or the "no extension" assumption has expired).
 */
export function isExtensionDetected() {
  if (cachedAdapters !== null) return true
  if (Date.now() < noExtensionUntil) return false
  return null
}

export function fetchAdapters({ timeoutMs = DEFAULT_TIMEOUT_MS, force = false } = {}) {
  if (!force) {
    if (cachedAdapters !== null) return Promise.resolve(cachedAdapters)
    if (Date.now() < noExtensionUntil) return Promise.resolve([])
  }
  if (inflight) return inflight

  inflight = requestFromExtension({ type: 'GET_ADAPTERS' }, 'ADAPTERS_RESPONSE', timeoutMs)
    .then(resp => {
      const before = cachedAdapters
      if (resp && Array.isArray(resp.adapters)) {
        cachedAdapters = resp.adapters
        noExtensionUntil = 0
        if (JSON.stringify(before) !== JSON.stringify(cachedAdapters)) notifyListeners()
        return cachedAdapters
      }
      cachedAdapters = null
      noExtensionUntil = Date.now() + NO_EXTENSION_RECHECK_MS
      if (before !== null) notifyListeners()
      scheduleEarlyRecheck(timeoutMs)
      return []
    })
    .finally(() => { inflight = null })
  return inflight
}

/** Forget what we know and ask the extension again. */
export function refreshAdapters(opts = {}) {
  cachedAdapters = null
  noExtensionUntil = 0
  return fetchAdapters({ ...opts, force: true })
}

/** Test helper: clear all module state. */
export function resetAdapterCache() {
  cachedAdapters = null
  noExtensionUntil = 0
  inflight = null
  earlyRecheckDone = false
  clearTimeout(earlyRecheckTimer)
  earlyRecheckTimer = null
  listeners.clear()
}

export function getCachedAdapters() {
  return cachedAdapters || []
}

export async function getAdapterForUrl(urlOrHost) {
  const host = hostnameFromUrl(urlOrHost)
  if (!host) return null

  const adapters = await fetchAdapters()
  if (!adapters || adapters.length === 0) return null

  return adapters.find(a => matchDescriptorMatch(host, a.matchDescriptor)) || null
}

export async function hasAdapterForUrl(urlOrHost) {
  const a = await getAdapterForUrl(urlOrHost)
  if (a) return true

  // No reply to GET_ADAPTERS: no extension, so don't wait on another timeout.
  if (isExtensionDetected() !== true) return false

  // The extension is present but no descriptor matched; let it decide with
  // its own matcher (it answers quickly when installed).
  const resp = await requestFromExtension({ type: 'HAS_ADAPTER', url: urlOrHost }, 'HAS_ADAPTER_RESPONSE', HAS_ADAPTER_TIMEOUT_MS)
  return !!(resp && resp.exists === true)
}

export default { fetchAdapters, refreshAdapters, subscribeAdapters, isExtensionDetected, getCachedAdapters, getAdapterForUrl, hasAdapterForUrl }
