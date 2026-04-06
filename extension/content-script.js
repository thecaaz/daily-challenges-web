// ScoreBridge PoC content script
;(function () {
  'use strict'
  // --- Adapters (PoC built-in) ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      selectors: ['#totalText', '#totalScoreBreakdownText'],
      onInit: function (doc, ctx) {
        try {
          const btn = doc.querySelector('#tg-embed-continue')
          if (btn && typeof btn.click === 'function') btn.click()
        } catch (e) {}
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
      },
      captureSelector: function () {
        return '#resultsContainer'
      }
    }
  ]

  function findAdapter(host) {
    return ADAPTERS.find(a => a.match && a.match(host))
  }

  // Call adapter onInit if present
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

  // --- Messaging: respond to parent GET_SCORE and CAPTURE/REQUEST_IFRAME_CAPTURE requests ---
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
            const selector = ev.data.selector || null
            const nonce = ev.data.nonce
            let el = null
            try {
              el = selector ? document.querySelector(selector) : null
            } catch (e) {
              el = null
            }
            let rect
            if (el) {
              const r = el.getBoundingClientRect()
              rect = {
                left: r.left,
                top: r.top,
                width: r.width,
                height: r.height
              }
            } else {
              const w =
                document.documentElement.clientWidth || window.innerWidth || 0
              const h =
                document.documentElement.clientHeight || window.innerHeight || 0
              rect = { left: 0, top: 0, width: w, height: h }
            }
            try {
              window.top.postMessage(
                { type: 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE', nonce, rect },
                '*'
              )
            } catch (e) {}
          } catch (e) {}
          return
        }

        // Visible-tab capture request from an inner frame: only the top frame should handle
        if (ev.data.type === 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE') {
          try {
            if (window.top !== window) return
            const nonce = ev.data.nonce
            const childRect = ev.data.rect || {
              left: 0,
              top: 0,
              width: 0,
              height: 0
            }
            // Identify the iframe element that corresponds to ev.source
            let iframeEl = null
            try {
              const iframes = document.querySelectorAll('iframe')
              for (const f of iframes) {
                try {
                  if (f.contentWindow === ev.source) {
                    iframeEl = f
                    break
                  }
                } catch (e) {}
              }
            } catch (e) {}

            if (!iframeEl) {
              try {
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
              } catch (e) {}
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

            try {
              chrome.runtime.sendMessage(
                { type: 'CAPTURE_VISIBLE_TAB', rect: abs, dpr },
                resp => {
                  try {
                    if (!resp || !resp.ok || !resp.dataUrl) {
                      const payloadErr = {
                        type: 'CAPTURE_RESPONSE',
                        nonce,
                        error:
                          resp && resp.error ? resp.error : 'capture failed'
                      }
                      try {
                        window.postMessage(payloadErr, '*')
                      } catch (e) {}
                      try {
                        iframeEl.contentWindow.postMessage(
                          payloadErr,
                          ev.origin || '*'
                        )
                      } catch (e) {}
                      return
                    }
                    const img = new Image()
                    img.onload = function () {
                      try {
                        const sx = Math.round(abs.left * dpr)
                        const sy = Math.round(abs.top * dpr)
                        const sw = Math.max(1, Math.round(abs.width * dpr))
                        const sh = Math.max(1, Math.round(abs.height * dpr))
                        const out = document.createElement('canvas')
                        out.width = sw
                        out.height = sh
                        const ctx = out.getContext('2d')
                        ctx.drawImage(img, sx, sy, sw, sh, 0, 0, sw, sh)
                        const final = out.toDataURL('image/png')
                        const payloadOk = {
                          type: 'CAPTURE_RESPONSE',
                          nonce,
                          dataUrl: final
                        }
                        try {
                          window.postMessage(payloadOk, '*')
                        } catch (e) {}
                        try {
                          iframeEl.contentWindow.postMessage(
                            payloadOk,
                            ev.origin || '*'
                          )
                        } catch (e) {}
                      } catch (err) {
                        const payloadErr2 = {
                          type: 'CAPTURE_RESPONSE',
                          nonce,
                          error: String(err)
                        }
                        try {
                          window.postMessage(payloadErr2, '*')
                        } catch (e) {}
                        try {
                          iframeEl.contentWindow.postMessage(
                            payloadErr2,
                            ev.origin || '*'
                          )
                        } catch (e) {}
                      }
                    }
                    img.onerror = function () {
                      try {
                        window.postMessage(
                          {
                            type: 'CAPTURE_RESPONSE',
                            nonce,
                            error: 'image load error'
                          },
                          '*'
                        )
                      } catch (e) {}
                      try {
                        iframeEl.contentWindow.postMessage(
                          {
                            type: 'CAPTURE_RESPONSE',
                            nonce,
                            error: 'image load error'
                          },
                          ev.origin || '*'
                        )
                      } catch (e) {}
                    }
                    img.src = resp.dataUrl
                  } catch (e) {
                    const payloadErr3 = {
                      type: 'CAPTURE_RESPONSE',
                      nonce,
                      error: String(e)
                    }
                    try {
                      window.postMessage(payloadErr3, '*')
                    } catch (o) {}
                    try {
                      iframeEl.contentWindow.postMessage(
                        payloadErr3,
                        ev.origin || '*'
                      )
                    } catch (o) {}
                  }
                }
              )
            } catch (e) {
              const payloadErr4 = {
                type: 'CAPTURE_RESPONSE',
                nonce,
                error: String(e)
              }
              try {
                window.postMessage(payloadErr4, '*')
              } catch (o) {}
              try {
                iframeEl.contentWindow.postMessage(
                  payloadErr4,
                  ev.origin || '*'
                )
              } catch (o) {}
            }
          } catch (e) {}
          return
        }
        // message received
        const adapter = findAdapter(location.hostname) || null

        if (ev.data.type === 'GET_SCORE') {
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
