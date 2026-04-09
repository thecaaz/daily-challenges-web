// ScoreBridge-PoC content script
;(function () {
  'use strict'
  // --- Built-in site adapters ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      matchDescriptor: { type: 'includes', value: 'timeguessr' },
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      selectors: ['#totalText', '#totalScoreBreakdownText'],
      onInit: function (doc, ctx) {
      },
      readScore: function (doc) {
        const breakdownBtn = doc.querySelector('#breakdownButton')
        if (breakdownBtn) breakdownBtn.click()
        for (const s of this.selectors) {
          const el = doc.querySelector(s)
          if (!el) continue
          const text = (el.textContent || '').replace(/[^0-9.\-]/g, '').trim()
          if (!text) continue
          const n = Number(text)
          return Number.isNaN(n) ? text : n
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
          const text = (el.textContent || '').replace(/[^0-9.\-]/g, '').trim()
          if (!text) continue
          const n = Number(text)
          return Number.isNaN(n) ? text : n
        }
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
