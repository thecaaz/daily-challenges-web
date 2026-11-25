import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import {
  fetchAdapters,
  hasAdapterForUrl,
  getAdapterForUrl,
  refreshAdapters,
  resetAdapterCache,
  isExtensionDetected,
  subscribeAdapters,
  NO_EXTENSION_RECHECK_MS,
  EARLY_RECHECK_MS,
} from '../utils/adapters'

const ADAPTERS = [{ name: 'TimeGuessr', matchDescriptor: { type: 'includes', value: 'timeguessr' } }]
const REPLY_TIMEOUT_MS = 800

function reply(data, { source = window, origin = window.location.origin } = {}) {
  window.dispatchEvent(new MessageEvent('message', { data, source, origin }))
}

/** Simulates the extension's content script: answers requests posted to this window. */
function installExtension({ adapters = ADAPTERS, hasAdapter = false, source, origin } = {}) {
  return vi.spyOn(window, 'postMessage').mockImplementation(msg => {
    queueMicrotask(() => {
      if (msg.type === 'GET_ADAPTERS') reply({ type: 'ADAPTERS_RESPONSE', nonce: msg.nonce, adapters }, { source, origin })
      if (msg.type === 'HAS_ADAPTER') reply({ type: 'HAS_ADAPTER_RESPONSE', nonce: msg.nonce, exists: hasAdapter }, { source, origin })
    })
  })
}

function noExtension() {
  return vi.spyOn(window, 'postMessage').mockImplementation(() => {})
}

function fetchAndTimeOut() {
  const pending = fetchAdapters()
  return vi.advanceTimersByTimeAsync(REPLY_TIMEOUT_MS).then(() => pending)
}

function requestsOfType(spy, type) {
  return spy.mock.calls.filter(([msg]) => msg && msg.type === type)
}

describe('adapters', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    resetAdapterCache()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  describe('without the extension', () => {
    it('resolves [] once the first request times out', async () => {
      noExtension()

      const first = fetchAdapters()
      await vi.advanceTimersByTimeAsync(REPLY_TIMEOUT_MS)

      await expect(first).resolves.toEqual([])
      expect(isExtensionDetected()).toBe(false)
    })

    it('answers lookups without waiting or asking again once the extension is known to be missing', async () => {
      const spy = noExtension()
      await fetchAndTimeOut()

      await expect(hasAdapterForUrl('https://timeguessr.com/daily')).resolves.toBe(false)
      await expect(hasAdapterForUrl('https://example.com')).resolves.toBe(false)
      await expect(getAdapterForUrl('https://timeguessr.com')).resolves.toBeNull()
      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(1)
      expect(requestsOfType(spy, 'HAS_ADAPTER')).toHaveLength(0)
    })

    it('re-checks once shortly after the first miss, since content scripts may inject late', async () => {
      const spy = noExtension()
      await fetchAndTimeOut()

      await vi.advanceTimersByTimeAsync(EARLY_RECHECK_MS + REPLY_TIMEOUT_MS)

      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(2)
      expect(isExtensionDetected()).toBe(false)
    })

    it('forgets the miss after the recheck interval and asks again', async () => {
      const spy = noExtension()
      await fetchAndTimeOut()
      await vi.advanceTimersByTimeAsync(EARLY_RECHECK_MS + REPLY_TIMEOUT_MS)

      vi.advanceTimersByTime(NO_EXTENSION_RECHECK_MS + 1)
      expect(isExtensionDetected()).toBeNull()
      await fetchAndTimeOut()

      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(3)
    })

    it('keeps the recheck backoff after the one early re-check', async () => {
      const spy = noExtension()
      await fetchAndTimeOut()
      await vi.advanceTimersByTimeAsync(EARLY_RECHECK_MS + REPLY_TIMEOUT_MS)
      vi.advanceTimersByTime(NO_EXTENSION_RECHECK_MS + 1)
      await fetchAndTimeOut()

      await vi.advanceTimersByTimeAsync(EARLY_RECHECK_MS + REPLY_TIMEOUT_MS)

      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(3)
      await expect(hasAdapterForUrl('https://timeguessr.com')).resolves.toBe(false)
    })

    it('does not notify subscribers about a miss', async () => {
      noExtension()
      const listener = vi.fn()
      subscribeAdapters(listener)

      await fetchAndTimeOut()

      expect(listener).not.toHaveBeenCalled()
    })

    it('notifies subscribers when the early re-check finds an extension injected at document_idle', async () => {
      const missSpy = noExtension()
      const listener = vi.fn()
      subscribeAdapters(listener)
      await fetchAndTimeOut()
      missSpy.mockRestore()
      const spy = installExtension()

      await vi.advanceTimersByTimeAsync(EARLY_RECHECK_MS)

      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(1)
      expect(listener).toHaveBeenCalledTimes(1)
      expect(listener).toHaveBeenCalledWith(ADAPTERS)
      expect(isExtensionDetected()).toBe(true)
      await expect(hasAdapterForUrl('https://timeguessr.com/daily')).resolves.toBe(true)
    })

    it('unsubscribe stops notifications', async () => {
      noExtension()
      const listener = vi.fn()
      const unsubscribe = subscribeAdapters(listener)
      await fetchAndTimeOut()
      unsubscribe()

      vi.restoreAllMocks()
      installExtension()
      await vi.advanceTimersByTimeAsync(EARLY_RECHECK_MS)
      expect(isExtensionDetected()).toBe(true)
      expect(listener).not.toHaveBeenCalled()
    })

    it('refreshAdapters asks again immediately and picks up a newly installed extension', async () => {
      noExtension()
      await fetchAndTimeOut()
      vi.restoreAllMocks()

      installExtension()
      const listener = vi.fn()
      subscribeAdapters(listener)
      await expect(refreshAdapters()).resolves.toEqual(ADAPTERS)
      expect(isExtensionDetected()).toBe(true)
      expect(listener).toHaveBeenCalledTimes(1)
    })

    it('posts requests to its own origin, not "*"', async () => {
      const spy = noExtension()
      fetchAdapters()
      expect(spy).toHaveBeenCalledWith(expect.objectContaining({ type: 'GET_ADAPTERS' }), window.location.origin)
      await vi.advanceTimersByTimeAsync(REPLY_TIMEOUT_MS)
    })
  })

  describe('in-flight dedupe', () => {
    it('shares one GET_ADAPTERS request between concurrent callers', async () => {
      const spy = noExtension()
      const a = fetchAdapters()
      const b = fetchAdapters()
      const c = hasAdapterForUrl('https://timeguessr.com')
      expect(a).toBe(b)
      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(1)

      await vi.advanceTimersByTimeAsync(REPLY_TIMEOUT_MS)
      await expect(a).resolves.toEqual([])
      await expect(c).resolves.toBe(false)
      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(1)
    })
  })

  describe('with the extension', () => {
    it('matches adapters from the GET_ADAPTERS reply and caches them', async () => {
      const spy = installExtension()
      await expect(hasAdapterForUrl('https://timeguessr.com/roundonedaily')).resolves.toBe(true)
      await expect(getAdapterForUrl('https://timeguessr.com')).resolves.toEqual(ADAPTERS[0])
      expect(requestsOfType(spy, 'GET_ADAPTERS')).toHaveLength(1)
    })

    it('falls back to HAS_ADAPTER only when the extension is present', async () => {
      const spy = installExtension({ hasAdapter: true })
      await expect(hasAdapterForUrl('https://other-game.example')).resolves.toBe(true)
      expect(requestsOfType(spy, 'HAS_ADAPTER')).toHaveLength(1)
    })

    it('ignores replies from another source', async () => {
      installExtension({ source: null })

      await expect(fetchAndTimeOut()).resolves.toEqual([])
    })

    it('ignores replies from another origin', async () => {
      installExtension({ origin: 'https://evil.example' })

      await expect(fetchAndTimeOut()).resolves.toEqual([])
    })
  })
})
