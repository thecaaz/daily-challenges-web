/**
 * Asks the ScoreBridge browser extension for its adapters via
 * window.postMessage (the extension's content script runs in this window).
 *
 * - Concurrent fetchAdapters() calls share one in-flight request.
 * - When the extension doesn't answer GET_ADAPTERS, it is assumed absent and
 *   every lookup returns "no adapter" immediately, without another timeout.
 *   That assumption is re-checked after NO_EXTENSION_RECHECK_MS, or at once
 *   via refreshAdapters() (e.g. on the extension install page).
 * - The very first miss is re-checked once after EARLY_RECHECK_MS: on a hard
 *   load the content script is injected at document_idle and may not be
 *   listening yet when the app first asks.
 * - subscribeAdapters(listener) is called whenever the answer changes (e.g.
 *   the extension showed up), so hooks can look their adapter up again.
 */
import { MessageType } from './scoreBridge'
import { requestFromExtension } from './extensionRequest'
import { hostnameFromUrl, findAdapterForHost } from './adapterMatching'

const DEFAULT_TIMEOUT_MS = 800
const HAS_ADAPTER_TIMEOUT_MS = 300
export const NO_EXTENSION_RECHECK_MS = 30_000
export const EARLY_RECHECK_MS = 1500

/** Array once the extension has answered, null before. */
let cachedAdapters = null
/** While Date.now() is before this, assume there is no extension. */
let noExtensionUntil = 0
let inflight = null
let earlyRecheckDone = false
let earlyRecheckTimer = null
const listeners = new Set()

const ignoreListenerError = () => {}

/** Call `listener` whenever the known adapters change. Returns an unsubscribe function. */
export function subscribeAdapters(listener) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

function notifyListeners() {
  for (const listener of Array.from(listeners)) {
    try {
      listener(cachedAdapters || [])
    } catch (error) {
      ignoreListenerError(error)
    }
  }
}

function scheduleEarlyRecheck(timeoutMs) {
  if (earlyRecheckDone || typeof setTimeout !== 'function') return
  earlyRecheckDone = true
  earlyRecheckTimer = setTimeout(() => {
    earlyRecheckTimer = null
    const isStillUndetected = cachedAdapters === null
    if (isStillUndetected) fetchAdapters({ timeoutMs, force: true })
  }, EARLY_RECHECK_MS)
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

  inflight = requestFromExtension({ type: MessageType.GET_ADAPTERS }, MessageType.ADAPTERS_RESPONSE, timeoutMs)
    .then(reply => (Array.isArray(reply?.adapters) ? rememberAdapters(reply.adapters) : rememberNoExtension(timeoutMs)))
    .finally(() => { inflight = null })
  return inflight
}

function rememberAdapters(adapters) {
  const previous = cachedAdapters
  cachedAdapters = adapters
  noExtensionUntil = 0
  if (JSON.stringify(previous) !== JSON.stringify(cachedAdapters)) notifyListeners()
  return cachedAdapters
}

function rememberNoExtension(timeoutMs) {
  const wasDetected = cachedAdapters !== null
  cachedAdapters = null
  noExtensionUntil = Date.now() + NO_EXTENSION_RECHECK_MS
  if (wasDetected) notifyListeners()
  scheduleEarlyRecheck(timeoutMs)
  return []
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

  return findAdapterForHost(adapters, host)
}

/**
 * Whether an adapter exists for the URL. Without an answer to GET_ADAPTERS
 * there is no extension, so this doesn't wait on another timeout; when the
 * extension is present but none of its descriptors matched, it decides with
 * its own matcher (it answers quickly when installed).
 */
export async function hasAdapterForUrl(urlOrHost) {
  if (await getAdapterForUrl(urlOrHost)) return true
  if (isExtensionDetected() !== true) return false

  const reply = await requestFromExtension(
    { type: MessageType.HAS_ADAPTER, url: urlOrHost },
    MessageType.HAS_ADAPTER_RESPONSE,
    HAS_ADAPTER_TIMEOUT_MS
  )
  return reply?.exists === true
}

export default { fetchAdapters, refreshAdapters, subscribeAdapters, isExtensionDetected, getCachedAdapters, getAdapterForUrl, hasAdapterForUrl }
