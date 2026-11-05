// ScoreBridge content script.
//
// Runs in every frame of the app and of the supported game sites. Requires
// app-origins.js (globalThis.ScoreBridgeOrigins) and scoreparse.js
// (globalThis.ScoreParse) to be loaded first, see manifest.json.
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

  const { readNumber } = globalThis.ScoreParse
  const { isAppOrigin } = globalThis.ScoreBridgeOrigins

  const IS_TOP = window.top === window
  const IN_APP_TOP = IS_TOP && isAppOrigin(location.origin)

  const ARM_TTL_MS = 5000
  const CUTLE_RATIO_TIMEOUT_MS = 500

  function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms))
  }

  function finiteOrNull(n) {
    return typeof n === 'number' && Number.isFinite(n) ? n : null
  }

  // --- Cutle preciseRatio bridge ---
  // The page keeps the ratio in a page-scoped `let`, which the isolated
  // content-script world cannot read. An injected page script answers each
  // CUTLE_REQUEST_RATIO with the current value, so a score is never read
  // from a stale cache.
  const cutleWaiters = []

  window.addEventListener('message', ev => {
    if (ev.source !== window || ev.origin !== location.origin) return
    const data = ev.data
    if (!data || data.type !== 'CUTLE_PRECISE_RATIO') return
    const ratio = data.ratio
    const valid = typeof ratio === 'number' && ratio >= 0 && ratio <= 1
    while (cutleWaiters.length) cutleWaiters.shift()(valid ? ratio : null)
  })

  function requestCutleRatio() {
    return new Promise(resolve => {
      let done = false
      const finish = value => {
        if (done) return
        done = true
        resolve(value)
      }
      cutleWaiters.push(finish)
      setTimeout(() => finish(null), CUTLE_RATIO_TIMEOUT_MS)
      try {
        window.postMessage({ type: 'CUTLE_REQUEST_RATIO' }, location.origin)
      } catch (e) {
        finish(null)
      }
    })
  }

  // --- Built-in site adapters ---
  // readScore may return a number, null, or a promise of either.
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      matchDescriptor: { type: 'includes', value: 'timeguessr' },
      match: host => host.includes('timeguessr'),
      selectors: ['.summary-score'],
      readFromDom: function (doc) {
        for (const s of this.selectors) {
          const el = doc.querySelector(s)
          if (!el) continue
          // TimeGuessr scores are whole numbers in 0..50000, so any separator
          // in the rendered text is a grouping separator.
          const text = String(el.textContent).replace(/\u2018/g, "'")
          const n = readNumber(text, { integerOnly: true })
          if (n !== null) return n
        }
        return null
      },
      readScore: async function (doc) {
        // Read first: clicking the breakdown button re-renders the summary.
        let n = this.readFromDom(doc)
        const breakdownBtn = doc.querySelector('#breakdownButton')
        if (breakdownBtn) breakdownBtn.click()
        if (n === null) {
          await delay(150)
          n = this.readFromDom(doc)
        }
        return n
      }
    },
    {
      name: 'MapTap',
      matchDescriptor: { type: 'includes', value: 'maptap' },
      match: host => host.includes('maptap'),
      selectors: ['#ui_score'],
      readScore: function (doc) {
        for (const s of this.selectors) {
          const el = doc.querySelector(s)
          if (!el) continue
          const n = readNumber(el.textContent)
          if (n !== null) return n
        }
        return null
      }
    },
    {
      name: 'Cutle',
      matchDescriptor: { type: 'includes', value: 'pfiffel' },
      match: host => host.includes('pfiffel'),
      onInit: function (doc) {
        const scriptId = 'cutle-precise-ratio-injector'
        if (doc.getElementById(scriptId)) return
        const script = doc.createElement('script')
        script.id = scriptId
        script.textContent = `
          (function () {
            if (window.__scoreBridgeCutle) return
            window.__scoreBridgeCutle = true
            window.addEventListener('message', function (ev) {
              if (ev.source !== window || !ev.data || ev.data.type !== 'CUTLE_REQUEST_RATIO') return
              var ratio = null
              try { ratio = typeof preciseRatio !== 'undefined' ? preciseRatio : null } catch (e) {}
              window.postMessage({ type: 'CUTLE_PRECISE_RATIO', ratio: ratio }, location.origin)
            })
          })()
        `
        ;(doc.head || doc.documentElement || doc.body).appendChild(script)
      },
      readScore: async function (doc) {
        function extractTwoNumbers(text) {
          if (!text) return null
          const matches = text.match(/-?\d+(?:\.\d+)?/g)
          if (!matches || matches.length < 2) return null
          const a = Number(matches[0])
          const b = Number(matches[1])
          if (Number.isNaN(a) || Number.isNaN(b)) return null
          return [a, b]
        }

        function readPair(container) {
          if (!container) return null
          const leEl = container.querySelector('.le')
          const cutEl = container.querySelector('.cut')
          if (leEl && cutEl) {
            const a = Number((leEl.textContent || '').replace(/[^0-9.\-]/g, '').trim())
            const b = Number((cutEl.textContent || '').replace(/[^0-9.\-]/g, '').trim())
            if (!Number.isNaN(a) && !Number.isNaN(b)) return Math.min(a, b)
          }
          const parsed = extractTwoNumbers(container.textContent || '')
          if (parsed) return Math.min(parsed[0], parsed[1])
          return null
        }

        const ratio = await requestCutleRatio()
        if (ratio !== null) {
          const smaller = Math.min(ratio, 1 - ratio)
          return Math.round(smaller * 10000) / 100
        }

        const fromResult = readPair(doc.querySelector('#result'))
        if (fromResult !== null) return fromResult
        const fromStats = readPair(doc.querySelector('#statsScore'))
        if (fromStats !== null) return fromStats
        return null
      }
    },
    {
      name: 'BlindCut',
      matchDescriptor: { type: 'includes', value: 'blindcut' },
      match: host => host.includes('blindcut'),
      readScore: function (doc) {
        // The game records the daily result in localStorage the moment the
        // final screen mounts, keyed by UTC date. Prefer it over the DOM:
        // the on-screen number runs a ~1s count-up animation.
        try {
          const key = 'blind-cut-daily-best:' + new Date().toISOString().slice(0, 10)
          const stored = window.localStorage.getItem(key)
          if (stored !== null) {
            const n = Number(stored)
            if (Number.isFinite(n)) return Math.round(n * 100) / 100
          }
        } catch (e) {}

        // Fallback: total on the final screen (out of 500), e.g. "437.25".
        // Genuinely fractional, so no integerOnly here.
        const finalEl = doc.querySelector('.final-screen .final-score strong')
        if (finalEl) {
          const n = readNumber(finalEl.textContent)
          if (n !== null) return n
        }
        return null
      }
    },
    {
      name: 'MotionPath',
      matchDescriptor: { type: 'includes', value: 'motionpath' },
      match: host => host.includes('motionpath'),
      readScore: function (doc) {
        // Final screen total (out of 500). The <strong> runs a count-up
        // animation, but the aria-label carries the exact total immediately.
        const scoreEl = doc.querySelector('.final-screen .final-score')
        if (scoreEl) {
          const labelScore = readNumber(scoreEl.getAttribute('aria-label') || '')
          if (labelScore !== null) return labelScore
          const strongEl = scoreEl.querySelector('strong')
          if (strongEl) {
            const n = readNumber(strongEl.textContent)
            if (n !== null) return n
          }
        }

        // Fallback: today's recorded completion for the active game.
        // MotionPath hosts four games selected via the ?game= query param,
        // each with its own daily-state localStorage key.
        try {
          const DAILY_KEYS = {
            path: 'motion-path.daily.v1',
            dots: 'dot-memory.daily.v1',
            rhythm: 'rhythm.daily.v1',
            cut: 'blind-cut.daily.v1'
          }
          const game = new URL(location.href).searchParams.get('game') || 'path'
          const raw = window.localStorage.getItem(DAILY_KEYS[game] || DAILY_KEYS.path)
          if (raw) {
            const data = JSON.parse(raw)
            const today = new Date().toISOString().slice(0, 10)
            const entry = data && data.completions && data.completions[today]
            if (entry && typeof entry.score === 'number' && Number.isFinite(entry.score)) {
              return entry.score
            }
          }
        } catch (e) {}
        return null
      }
    }
  ]

  function findAdapter(host) {
    return ADAPTERS.find(a => a.match && a.match(String(host || '').toLowerCase())) || null
  }

  // Run the adapter-specific startup hook when the current host matches.
  ;(function runAdapterInit() {
    try {
      const adapter = findAdapter(location.hostname)
      if (adapter && typeof adapter.onInit === 'function') adapter.onInit(document)
    } catch (err) {}
  })()

  // --- Capture arming (app top frame only) ---
  // A capture runs only for a nonce the app armed from its own window within
  // the last ARM_TTL_MS. Each armed nonce is good for one capture.
  let armed = null

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
    return typeof nonce === 'string' && nonce.length >= 8 && nonce.length <= 128
  }

  function postToApp(payload) {
    // Same window only; never to an iframe, never to '*'.
    window.postMessage(payload, location.origin)
  }

  function replyTo(ev, payload) {
    try {
      ev.source.postMessage(payload, ev.origin)
    } catch (e) {}
  }

  function findIframeFor(source) {
    if (!source || source === window) return null
    for (const f of document.querySelectorAll('iframe')) {
      if (f.contentWindow === source) return f
    }
    return null
  }

  // Area of the iframe's viewport in page (document) CSS pixels, which is
  // what tabs.captureTab's rect expects.
  function captureRectFor(iframeEl, childRect) {
    const r = iframeEl.getBoundingClientRect()
    let width = iframeEl.clientWidth || r.width
    let height = iframeEl.clientHeight || r.height
    // The child reports its own viewport size (excludes its scrollbars);
    // it may only shrink the rect, never grow it.
    if (childRect && Number.isFinite(childRect.width) && childRect.width > 0) {
      width = Math.min(width, childRect.width)
    }
    if (childRect && Number.isFinite(childRect.height) && childRect.height > 0) {
      height = Math.min(height, childRect.height)
    }
    return {
      x: r.left + iframeEl.clientLeft + window.scrollX,
      y: r.top + iframeEl.clientTop + window.scrollY,
      width,
      height
    }
  }

  async function handleCaptureRequest(ev) {
    const nonce = ev.data.nonce
    const iframeEl = findIframeFor(ev.source)
    if (!iframeEl) return // not from a frame of this page
    if (!consumeArm(nonce)) {
      console.warn('ScoreBridge: ignoring capture request without an armed nonce')
      return
    }

    const rect = captureRectFor(iframeEl, ev.data.rect)
    const dpr = window.devicePixelRatio || 1
    let resp
    try {
      resp = await browser.runtime.sendMessage({ type: 'CAPTURE_TAB', rect })
    } catch (e) {
      resp = { ok: false, error: String((e && e.message) || e) }
    }

    if (!resp || !resp.ok || !resp.dataUrl) {
      postToApp({
        type: 'CAPTURE_RESPONSE',
        nonce,
        error: resp && resp.error ? resp.error : 'capture failed'
      })
      return
    }
    postToApp({
      type: 'CAPTURE_RESPONSE',
      nonce,
      dataUrl: resp.dataUrl,
      cropped: !!resp.cropped,
      rect,
      dpr
    })
  }

  window.addEventListener('message', async ev => {
    try {
      const data = ev.data
      if (!data || typeof data.type !== 'string') return

      switch (data.type) {
        // App arms one capture from its own top window.
        case 'SCOREBRIDGE_ARM': {
          if (!IN_APP_TOP) return
          if (ev.source !== window || ev.origin !== location.origin) return
          if (!isValidNonce(data.nonce)) return
          arm(data.nonce)
          return
        }

        // Game iframe forwards the app's capture request to the app frame.
        case 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE': {
          if (!IN_APP_TOP) return
          if (!isValidNonce(data.nonce)) return
          await handleCaptureRequest(ev)
          return
        }

        // App (parent) asks this game iframe to request a capture of itself.
        case 'REQUEST_VISIBLE_TAB_FROM_PARENT': {
          if (IS_TOP) return
          if (ev.source !== window.parent || !isAppOrigin(ev.origin)) return
          if (!isValidNonce(data.nonce)) return
          const w = document.documentElement.clientWidth || window.innerWidth || 0
          const h = document.documentElement.clientHeight || window.innerHeight || 0
          window.parent.postMessage(
            {
              type: 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE',
              nonce: data.nonce,
              rect: { width: w, height: h }
            },
            ev.origin
          )
          return
        }

        case 'HAS_ADAPTER': {
          if (!isAppOrigin(ev.origin) || !ev.source) return
          let adapter = null
          try {
            const urlStr = data.url || ''
            let host
            try {
              host = new URL(urlStr).hostname
            } catch (e) {
              host = urlStr || location.hostname
            }
            adapter = findAdapter(host)
          } catch (e) {}
          replyTo(ev, {
            type: 'HAS_ADAPTER_RESPONSE',
            nonce: data.nonce,
            exists: !!adapter,
            adapterName: adapter ? adapter.name : null
          })
          return
        }

        case 'GET_ADAPTERS': {
          if (!isAppOrigin(ev.origin) || !ev.source) return
          replyTo(ev, {
            type: 'ADAPTERS_RESPONSE',
            nonce: data.nonce,
            adapters: ADAPTERS.map(a => ({ name: a.name, matchDescriptor: a.matchDescriptor }))
          })
          return
        }

        case 'GET_SCORE': {
          if (!isAppOrigin(ev.origin) || !ev.source) return
          const adapter = findAdapter(location.hostname)
          let score = null
          if (adapter) {
            try {
              score = finiteOrNull(await adapter.readScore(document))
            } catch (e) {
              console.error('ScoreBridge: readScore failed', e)
            }
          }
          replyTo(ev, { type: 'SCORE_RESPONSE', nonce: data.nonce, score })
          return
        }

        default:
          return
      }
    } catch (err) {
      console.error('ScoreBridge: error handling message', err)
    }
  })
})()
