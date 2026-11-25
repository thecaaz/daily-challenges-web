// ScoreBridge content script.
//
// Runs in every frame of the app and of the supported game sites. Requires
// app-origins.js (globalThis.ScoreBridgeOrigins), scoreparse.js and
// site-adapters.js (globalThis.ScoreBridgeAdapters) to be loaded first, see
// manifest.json.
//
// Message flow (all window.postMessage):
//   app top frame  --SCOREBRIDGE_ARM {nonce}-->            app top frame (this script)
//   app top frame  --GET_SCORE {nonce}-->                  game iframe
//   game iframe    --SCORE_RESPONSE {nonce, score}-->      app top frame
//   app top frame  --REQUEST_VISIBLE_TAB_FROM_PARENT-->    game iframe
//   game iframe    --SCOREBRIDGE_VISIBLE_TAB_CAPTURE-->    app top frame (this script)
//   this script    --CAPTURE_RESPONSE {nonce, ...}-->      app top frame (same window only)
;(function () {
  'use strict'

  const { isAppOrigin } = globalThis.ScoreBridgeOrigins
  const { findAdapter, describeAdapters } = globalThis.ScoreBridgeAdapters

  const IS_TOP = window.top === window
  const IN_APP_TOP = IS_TOP && isAppOrigin(location.origin)

  const ARM_TTL_MS = 5000
  const MIN_NONCE_LENGTH = 8
  const MAX_NONCE_LENGTH = 128
  const DEFAULT_DEVICE_PIXEL_RATIO = 1
  const CAPTURE_FAILED = 'capture failed'
  const UNARMED_CAPTURE_WARNING = 'ScoreBridge: ignoring capture request without an armed nonce'

  const MessageType = Object.freeze({
    ARM_CAPTURE: 'SCOREBRIDGE_ARM',
    VISIBLE_TAB_CAPTURE: 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE',
    REQUEST_VISIBLE_TAB: 'REQUEST_VISIBLE_TAB_FROM_PARENT',
    CAPTURE_RESPONSE: 'CAPTURE_RESPONSE',
    CAPTURE_TAB: 'CAPTURE_TAB',
    HAS_ADAPTER: 'HAS_ADAPTER',
    HAS_ADAPTER_RESPONSE: 'HAS_ADAPTER_RESPONSE',
    GET_ADAPTERS: 'GET_ADAPTERS',
    ADAPTERS_RESPONSE: 'ADAPTERS_RESPONSE',
    GET_SCORE: 'GET_SCORE',
    SCORE_RESPONSE: 'SCORE_RESPONSE',
  })

  const MESSAGE_HANDLERS = new Map([
    [MessageType.ARM_CAPTURE, handleArmCapture],
    [MessageType.VISIBLE_TAB_CAPTURE, handleVisibleTabCapture],
    [MessageType.REQUEST_VISIBLE_TAB, handleRequestVisibleTab],
    [MessageType.HAS_ADAPTER, handleHasAdapter],
    [MessageType.GET_ADAPTERS, handleGetAdapters],
    [MessageType.GET_SCORE, handleGetScore],
  ])

  /** An adapter's onInit failing must not stop the message protocol. */
  const ignoreAdapterInitError = () => {}
  /** The requesting window may be gone by the time the reply is ready. */
  const ignoreUnreachableRequester = () => {}

  /**
   * A capture runs only for a nonce the app armed from its own window within
   * the last ARM_TTL_MS. Each armed nonce is good for one capture.
   */
  let armed = null

  runAdapterInit()
  window.addEventListener('message', handleMessage)

  function runAdapterInit() {
    try {
      const adapter = findAdapter(location.hostname)
      if (adapter && typeof adapter.onInit === 'function') adapter.onInit(document)
    } catch (initError) {
      ignoreAdapterInitError(initError)
    }
  }

  async function handleMessage(ev) {
    try {
      const data = ev.data
      if (!data || typeof data.type !== 'string') return
      const handler = MESSAGE_HANDLERS.get(data.type)
      if (handler) await handler(ev, data)
    } catch (err) {
      console.error('ScoreBridge: error handling message', err)
    }
  }

  /** The app arms one capture from its own top window. */
  function handleArmCapture(ev, data) {
    if (!IN_APP_TOP) return
    if (ev.source !== window || ev.origin !== location.origin) return
    if (!isValidNonce(data.nonce)) return
    arm(data.nonce)
  }

  /** A game iframe forwards the app's capture request to the app frame. */
  async function handleVisibleTabCapture(ev, data) {
    if (!IN_APP_TOP) return
    if (!isValidNonce(data.nonce)) return
    await captureRequestingIframe(ev)
  }

  /** The app (parent) asks this game iframe to request a capture of itself. */
  function handleRequestVisibleTab(ev, data) {
    if (IS_TOP) return
    if (ev.source !== window.parent || !isAppOrigin(ev.origin)) return
    if (!isValidNonce(data.nonce)) return
    window.parent.postMessage(
      { type: MessageType.VISIBLE_TAB_CAPTURE, nonce: data.nonce, rect: viewportSize() },
      ev.origin
    )
  }

  function handleHasAdapter(ev, data) {
    if (!isAppRequest(ev)) return
    const adapter = findAdapter(hostOf(data.url || ''))
    replyTo(ev, {
      type: MessageType.HAS_ADAPTER_RESPONSE,
      nonce: data.nonce,
      exists: !!adapter,
      adapterName: adapter ? adapter.name : null
    })
  }

  function handleGetAdapters(ev, data) {
    if (!isAppRequest(ev)) return
    replyTo(ev, { type: MessageType.ADAPTERS_RESPONSE, nonce: data.nonce, adapters: describeAdapters() })
  }

  async function handleGetScore(ev, data) {
    if (!isAppRequest(ev)) return
    const score = await readPageScore()
    replyTo(ev, { type: MessageType.SCORE_RESPONSE, nonce: data.nonce, score })
  }

  async function readPageScore() {
    const adapter = findAdapter(location.hostname)
    if (!adapter) return null
    try {
      return finiteOrNull(await adapter.readScore(document))
    } catch (e) {
      console.error('ScoreBridge: readScore failed', e)
      return null
    }
  }

  async function captureRequestingIframe(ev) {
    const nonce = ev.data.nonce
    const iframeElement = findIframeFor(ev.source)
    if (!iframeElement) return
    if (!consumeArm(nonce)) {
      console.warn(UNARMED_CAPTURE_WARNING)
      return
    }

    const rect = captureRectFor(iframeElement, ev.data.rect)
    const dpr = window.devicePixelRatio || DEFAULT_DEVICE_PIXEL_RATIO
    const capture = await requestTabCapture(rect)

    if (!capture || !capture.ok || !capture.dataUrl) {
      postToApp({ type: MessageType.CAPTURE_RESPONSE, nonce, error: capture && capture.error ? capture.error : CAPTURE_FAILED })
      return
    }
    postToApp({ type: MessageType.CAPTURE_RESPONSE, nonce, dataUrl: capture.dataUrl, cropped: !!capture.cropped, rect, dpr })
  }

  async function requestTabCapture(rect) {
    try {
      return await browser.runtime.sendMessage({ type: MessageType.CAPTURE_TAB, rect })
    } catch (e) {
      return { ok: false, error: String((e && e.message) || e) }
    }
  }

  function arm(nonce) {
    armed = { nonce, expires: Date.now() + ARM_TTL_MS }
  }

  function consumeArm(nonce) {
    if (!armed) return false
    if (Date.now() > armed.expires) {
      armed = null
      return false
    }
    if (typeof nonce !== 'string' || nonce !== armed.nonce) return false
    armed = null
    return true
  }

  function isValidNonce(nonce) {
    return typeof nonce === 'string' && nonce.length >= MIN_NONCE_LENGTH && nonce.length <= MAX_NONCE_LENGTH
  }

  function isAppRequest(ev) {
    return isAppOrigin(ev.origin) && !!ev.source
  }

  /** The hostname of `url`; anything that isn't a URL is taken as a host, '' as this page's host. */
  function hostOf(url) {
    try {
      return new URL(url).hostname
    } catch (notAUrl) {
      return url || location.hostname
    }
  }

  function finiteOrNull(n) {
    return typeof n === 'number' && Number.isFinite(n) ? n : null
  }

  /** Posts to this window only; never to an iframe, never to '*'. */
  function postToApp(payload) {
    window.postMessage(payload, location.origin)
  }

  function replyTo(ev, payload) {
    try {
      ev.source.postMessage(payload, ev.origin)
    } catch (unreachable) {
      ignoreUnreachableRequester(unreachable)
    }
  }

  function findIframeFor(source) {
    if (!source || source === window) return null
    for (const iframe of document.querySelectorAll('iframe')) {
      if (iframe.contentWindow === source) return iframe
    }
    return null
  }

  function viewportSize() {
    return {
      width: document.documentElement.clientWidth || window.innerWidth || 0,
      height: document.documentElement.clientHeight || window.innerHeight || 0
    }
  }

  /**
   * Area of the iframe's viewport in page (document) CSS pixels, which is what
   * tabs.captureTab's rect expects. The child reports its own viewport size
   * (without its scrollbars), which may only shrink the rect, never grow it.
   */
  function captureRectFor(iframeElement, childRect) {
    const bounds = iframeElement.getBoundingClientRect()
    const child = childRect || {}
    return {
      x: bounds.left + iframeElement.clientLeft + window.scrollX,
      y: bounds.top + iframeElement.clientTop + window.scrollY,
      width: shrunkTo(iframeElement.clientWidth || bounds.width, child.width),
      height: shrunkTo(iframeElement.clientHeight || bounds.height, child.height)
    }
  }

  function shrunkTo(length, childLength) {
    return Number.isFinite(childLength) && childLength > 0 ? Math.min(length, childLength) : length
  }
})()
