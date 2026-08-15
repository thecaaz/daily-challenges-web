// ScoreBridge-PoC content script
;(function () {
  'use strict'

  // Game pages render scores with the player's locale, so the same value shows
  // up as "40,456" on an en-US machine and "40.456" on a de-DE one. The
  // separators are indistinguishable by character, so mirror the rule in
  // frontend/src/utils/parseScore.js: the last '.' or ',' is a decimal point
  // only when fewer than three digits follow it; every other separator is a
  // grouping separator and gets dropped.
  //
  // Adapters for games that can only ever score whole numbers pass
  // { integerOnly: true }, which drops every separator unconditionally. That
  // matters for TimeGuessr, where a legitimate "40.456" would otherwise be read
  // as the decimal 40.456 on period-grouping locales.
  const SEPARATORS = /[.,\u00A0\u202F\s]/g
  const NUMERIC_TOKEN = /-?\d+(?:[.,\u00A0\u202F ]\d+)*/

  function readNumber(text, opts) {
    if (text == null) return null
    const token = String(text).match(NUMERIC_TOKEN)
    if (!token) return null

    let s = token[0]
    let sign = ''
    if (s[0] === '-') {
      sign = '-'
      s = s.slice(1)
    }

    let lastSep = -1
    for (let i = s.length - 1; i >= 0; i--) {
      if (s[i] === '.' || s[i] === ',') {
        lastSep = i
        break
      }
    }

    let normalized
    const after = lastSep === -1 ? '' : s.slice(lastSep + 1)
    if (lastSep !== -1 && !(opts && opts.integerOnly) && after.length > 0 && after.length < 3) {
      normalized = s.slice(0, lastSep).replace(SEPARATORS, '') + '.' + after
    } else {
      normalized = s.replace(SEPARATORS, '')
    }

    const n = Number(sign + normalized)
    return Number.isFinite(n) ? n : null
  }

  // --- Built-in site adapters ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      matchDescriptor: { type: 'includes', value: 'timeguessr' },
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      selectors: ['.summary-score'],
      onInit: function (doc, ctx) {
      },
      readScore: function (doc) {
        const breakdownBtn = doc.querySelector('#breakdownButton')
        if (breakdownBtn) breakdownBtn.click()
        for (const s of this.selectors) {
          const el = doc.querySelector(s)
          if (!el) continue
          // TimeGuessr scores are whole numbers in 0..50000, so any separator
          // in the rendered text is a grouping separator.
          const text = String(el.textContent).replace(/['\u2018\u2019]/g, '')
          const n = readNumber(text, { integerOnly: true })
          if (n === null) continue
          return n
        }
        return null
      }
    },
    {
      name: 'MapTap',
      matchDescriptor: { type: 'includes', value: 'maptap' },
      match: host => /maptap/.test(host) || host.includes('maptap'),
      selectors: ['#ui_score'],
      onInit: function (doc, ctx) {
      },
      readScore: function (doc) {
        for (const s of this.selectors) {
          const el = doc.querySelector(s)
          if (!el) continue
          const n = readNumber(el.textContent)
          if (n === null) continue
          return n
        }
        return null
      }
    },
    {
      name: 'Cutle',
      matchDescriptor: { type: 'includes', value: 'pfiffel' },
      match: host => /pfiffel/.test(host) || host.includes('pfiffel'),
      onInit: function (doc, ctx) {
        // Inject a script into the page to read preciseRatio (content scripts
        // run in an isolated world and cannot access page-scoped `let`/`const`).
        const scriptId = 'cutle-precise-ratio-injector'
        if (doc.getElementById(scriptId)) return

        const script = doc.createElement('script')
        script.id = scriptId
        script.textContent = `
          (function() {
            if (typeof _cutlePreciseRatio !== 'undefined') return
            function poll() {
              if (typeof preciseRatio !== 'undefined') {
                _cutlePreciseRatio = preciseRatio
                try { window.postMessage({ type: 'CUTLE_PRECISE_RATIO', ratio: preciseRatio }, '*') } catch(e) {}
              } else {
                setTimeout(poll, 300)
              }
            }
            poll()
          })()
        `
        ;(doc.head || doc.documentElement || doc.body).appendChild(script)
      },
      readScore: function (doc) {
        function extractTwoNumbers(text) {
          if (!text) return null
          const matches = text.match(/-?\d+(?:\.\d+)?/g)
          if (!matches || matches.length < 2) return null
          const a = Number(matches[0])
          const b = Number(matches[1])
          if (Number.isNaN(a) || Number.isNaN(b)) return null
          return [a, b]
        }

        // Try the cached ratio from the injected page script
        if (_cachedCutleRatio !== null) {
          const ratio = Math.min(_cachedCutleRatio, 1 - _cachedCutleRatio)
          return Math.round(ratio * 10000) / 100
        }

        const resultEl = doc.querySelector('#result')
        if (resultEl) {
          const leEl = resultEl.querySelector('.le')
          const cutEl = resultEl.querySelector('.cut')
          if (leEl && cutEl) {
            const a = Number((leEl.textContent || '').replace(/[^0-9.\-]/g, '').trim())
            const b = Number((cutEl.textContent || '').replace(/[^0-9.\-]/g, '').trim())
            if (!Number.isNaN(a) && !Number.isNaN(b)) return Math.min(a, b)
          }
          const parsed = extractTwoNumbers(resultEl.textContent || '')
          if (parsed) return Math.min(parsed[0], parsed[1])
        }

        const statsEl = doc.querySelector('#statsScore')
        if (statsEl) {
          const leEl = statsEl.querySelector('.le')
          const cutEl = statsEl.querySelector('.cut')
          if (leEl && cutEl) {
            const a = Number((leEl.textContent || '').replace(/[^0-9.\-]/g, '').trim())
            const b = Number((cutEl.textContent || '').replace(/[^0-9.\-]/g, '').trim())
            if (!Number.isNaN(a) && !Number.isNaN(b)) return Math.min(a, b)
          }
          const parsed = extractTwoNumbers(statsEl.textContent || '')
          if (parsed) return Math.min(parsed[0], parsed[1])
        }
      }
    },
    {
      name: 'BlindCut',
      matchDescriptor: { type: 'includes', value: 'blindcut' },
      match: host => /blindcut/.test(host) || host.includes('blindcut'),
      onInit: function (doc, ctx) {
      },
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
      match: host => /motionpath/.test(host) || host.includes('motionpath'),
      onInit: function (doc, ctx) {
      },
      readScore: function (doc) {
        // Final screen total (out of 500). The <strong> runs a count-up
        // animation, but the aria-label carries the exact total immediately.
        const scoreEl = doc.querySelector('.final-screen .final-score')
        if (scoreEl) {
          const label = scoreEl.getAttribute('aria-label') || ''
          const labelScore = readNumber(label)
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
    return ADAPTERS.find(a => a.match && a.match(host))
  }

  // Run the adapter-specific startup hook when the current host matches.
  ;(function runAdapterInit() {
    try {
      const adapter = findAdapter(location.hostname)
      if (!adapter || typeof adapter.onInit !== 'function') return
      const ctx = {
        host: location.hostname,
        href: location.href,
        inIframe: window.top !== window
      }
      const res = adapter.onInit(document, ctx)
      if (res && typeof res.then === 'function') res.catch(() => {})
    } catch (err) {}
  })()

  // --- Cutle preciseRatio cache (set by injected page script via postMessage) ---
  let _cachedCutleRatio = null
  window.addEventListener('message', function onCutleRatio(ev) {
    if (ev.data && ev.data.type === 'CUTLE_PRECISE_RATIO') {
      _cachedCutleRatio = ev.data.ratio
    }
  }, false)

  // Handle score, capture, and adapter discovery requests sent over window.postMessage.
  window.addEventListener(
    'message',
    async ev => {
      try {
        if (!ev.data || typeof ev.data.type !== 'string') return

        // Parent -> child asks the child to request a visible-tab capture from top
        if (ev.data.type === 'REQUEST_VISIBLE_TAB_FROM_PARENT') {
          try {
            // Only forward from within a frame (child)
            if (window.top === window) return
            let rect
            const w =
              document.documentElement.clientWidth || window.innerWidth || 0
            const h =
              document.documentElement.clientHeight || window.innerHeight || 0
            rect = { left: 0, top: 0, width: w, height: h }
            try {
              window.top.postMessage(
                {
                  type: 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE',
                  nonce: ev.data.nonce,
                  rect
                },
                '*'
              )
            } catch (e) {
              console.error('Error posting SCOREBRIDGE_VISIBLE_TAB_CAPTURE', e)
            }
          } catch (e) {
            console.error('Error handling REQUEST_VISIBLE_TAB_FROM_PARENT', e)
          }
          return
        }

        // Visible-tab capture request from an inner frame: only the top frame should handle
        if (ev.data.type === 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE') {
          if (window.top !== window) return
          try {
            const nonce = ev.data.nonce
            const childRect = ev.data.rect
            // Identify the iframe element that corresponds to ev.source
            let iframeEl = null
            const iframes = document.querySelectorAll('iframe')
            for (const f of iframes) {
              if (f.contentWindow === ev.source) {
                iframeEl = f
                break
              }
            }

            if (!iframeEl) {
              ev.source &&
                ev.source.postMessage &&
                ev.source.postMessage(
                  {
                    type: 'CAPTURE_RESPONSE',
                    nonce,
                    error: 'iframe element not found'
                  },
                  ev.origin || '*'
                )
              console.error(
                'Iframe element not found for SCOREBRIDGE_VISIBLE_TAB_CAPTURE'
              )
              return
            }

            const iframeRect = iframeEl.getBoundingClientRect()
            const abs = {
              left: iframeRect.left + (childRect.left || 0),
              top: iframeRect.top + (childRect.top || 0),
              width: childRect.width || iframeRect.width,
              height: childRect.height || iframeRect.height
            }
            const dpr = window.devicePixelRatio || 1

            chrome.runtime.sendMessage(
              { type: 'CAPTURE_VISIBLE_TAB', rect: abs, dpr },
              resp => {
                if (!resp || !resp.ok || !resp.dataUrl) {
                  const payloadErr = {
                    type: 'CAPTURE_RESPONSE',
                    nonce,
                    error: resp && resp.error ? resp.error : 'capture failed'
                  }
                  window.postMessage(payloadErr, '*')
                  return
                }
                const payloadOk = {
                  type: 'CAPTURE_RESPONSE',
                  nonce,
                  dataUrl: resp.dataUrl,
                  rect: abs,
                  dpr
                }
                window.postMessage(payloadOk, '*')
                iframeEl.contentWindow.postMessage(payloadOk, ev.origin || '*')
              }
            )
          } catch (e) {
            console.error('Error handling CAPTURE_VISIBLE_TAB', e)
          }
          return
        }

        // Page asks whether an adapter exists for a given URL (payload: { url, nonce })
        if (ev.data.type === 'HAS_ADAPTER') {
          const nonce = ev.data.nonce
          let exists = false
          let adapterName = null
          try {
            const urlStr = ev.data.url || ''
            let host
            try {
              host = new URL(urlStr).hostname
            } catch (e) {
              host = urlStr || location.hostname
            }
            const adapter = findAdapter(host)
            exists = !!adapter
            adapterName = adapter ? adapter.name || null : null
          } catch (e) {
            exists = false
          }
          const payload = { type: 'HAS_ADAPTER_RESPONSE', nonce, exists, adapterName }
          try {
            ev.source.postMessage(payload, ev.origin || '*')
          } catch (e) {}
          return
        }

        // Page requests a list of adapters (payload: { nonce })
        if (ev.data.type === 'GET_ADAPTERS') {
          const nonce = ev.data.nonce
          const adapters = ADAPTERS.map(a => ({
            name: a.name,
            matchDescriptor: a.matchDescriptor || (a.match ? { type: 'function', value: a.match.toString() } : null)
          }))
          const payload = { type: 'ADAPTERS_RESPONSE', nonce, adapters }
          try {
            ev.source.postMessage(payload, ev.origin || '*')
          } catch (e) {}
          return
        }

        if (ev.data.type === 'GET_SCORE') {
          const adapter = findAdapter(location.hostname) || null
          const nonce = ev.data.nonce
          const score = adapter ? adapter.readScore(document) : null
          const payload = { type: 'SCORE_RESPONSE', nonce, score }
          try {
            ev.source.postMessage(payload, ev.origin || '*')
          } catch (e) {
            console.error('Error posting SCORE_RESPONSE', e)
          }
          return
        }
      } catch (err) {
        console.error('Error handling message in content script', err)
      }
    },
    false
  )
})()
