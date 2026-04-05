// ScoreBridge PoC content script
;(function () {
  'use strict'
  console.debug('ScoreBridge: content script loaded', {
    href: location.href,
    host: location.hostname,
    isTop: window.top === window
  })
  const __scorebridge_debug = {
    html2canvasPong: null,
    ensureAttempts: 0,
    lastEnsureCall: Date.now(),
    bridgePong: false
  }
  const __scorebridge_pendingCaptures = new Map()

  // --- Adapters (PoC built-in) ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      selectors: ['#totalText', '.score-value', '.score'],
      onInit: function (doc, ctx) {
        try {
          console.debug('TimeGuessr adapter onInit', ctx)
          const btn = doc.querySelector('#tg-embed-continue')
          if (btn && typeof btn.click === 'function') btn.click()
        } catch (e) {
          console.warn('TimeGuessr onInit failed', e)
        }
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
      if (res && typeof res.then === 'function')
        res.catch(err =>
          console.error('ScoreBridge: adapter onInit async error', err)
        )
    } catch (err) {
      console.error('ScoreBridge: adapter onInit error', err)
    }
  })()

  // --- html2canvas helper: load vendor loader and wait for html2canvas to be available ---
  function ensureHtml2Canvas(timeoutMs = 5000) {
    console.debug('ScoreBridge: ensureHtml2Canvas start', {
      timeoutMs,
      href: location.href,
      host: location.hostname,
      docReadyState: document.readyState,
      isTop: window.top === window,
      inIframe: window.top !== window
    })
    // Note: html2canvas will live in the page context (not the isolated content-script
    // world). We cannot access it directly via `window.html2canvas` here. Instead we
    // rely on the page-context detection pongs and a small injected bridge that will
    // execute html2canvas in the page and return results via postMessage.
    if (window.html2canvas && typeof window.html2canvas === 'function') {
      console.debug(
        'ScoreBridge: html2canvas already present in content-script context',
        typeof window.html2canvas
      )
      // still return a resolved promise to keep compatibility with callers
      return Promise.resolve(true)
    }
    const vendorUrl = chrome.runtime.getURL('vendor/html2canvas.min.js')
    return new Promise((resolve, reject) => {
      try {
        // inject a lightweight page-context detector to see if html2canvas is present in the page world
        try {
          if (
            !document.querySelector(
              'script[data-scorebridge-html2canvas-detect]'
            )
          ) {
            const detect = document.createElement('script')
            detect.dataset.scorebridgeHtml2canvasDetect = '1'
            detect.textContent =
              "try{window.postMessage({ type: 'SCOREBRIDGE_DEBUG_HTML2CANVAS_PONG', source: 'detect_preinject', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){}"
            ;(document.head || document.documentElement).appendChild(detect)
            setTimeout(() => {
              try {
                detect.parentNode && detect.parentNode.removeChild(detect)
              } catch (e) {}
            }, 1000)
            console.debug(
              'ScoreBridge: injected page-context detection script (pre-inject)'
            )
          }
        } catch (detEx) {
          console.warn('ScoreBridge: detect injection failed', detEx)
        }

        if (!document.querySelector('script[data-scorebridge-html2canvas]')) {
          console.debug(
            'ScoreBridge: no existing html2canvas script found; injecting external script',
            { vendorUrl }
          )
          console.debug(
            'ScoreBridge: existing html2canvas script tags',
            Array.from(document.scripts)
              .filter(s => s.src && s.src.includes('html2canvas'))
              .map(s => s.src)
          )
          const s = document.createElement('script')
          s.dataset.scorebridgeHtml2canvas = '1'
          s.src = vendorUrl
          s.async = true
          s.onload = () => {
            console.debug(
              'ScoreBridge: vendor loader script loaded (onload). content-script sees html2canvas typeof:',
              typeof window.html2canvas
            )
            try {
              const detect = document.createElement('script')
              detect.dataset.scorebridgeHtml2canvasDetect = '1'
              detect.textContent =
                "try{window.postMessage({ type: 'SCOREBRIDGE_DEBUG_HTML2CANVAS_PONG', source: 'external_onload', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){}"
              ;(document.head || document.documentElement).appendChild(detect)
              setTimeout(() => {
                try {
                  detect.parentNode && detect.parentNode.removeChild(detect)
                } catch (e) {}
              }, 1000)
              console.debug(
                'ScoreBridge: injected page-context detection script (onload)'
              )
            } catch (detEx) {
              console.warn('ScoreBridge: onload detect injection failed', detEx)
            }
          }
          s.onerror = e => {
            console.warn(
              'ScoreBridge: html2canvas loader error, attempting fallback fetch',
              e
            )
            try {
              console.debug('ScoreBridge: fetch fallback for', vendorUrl)
              fetch(vendorUrl)
                .then(resp => {
                  console.debug('ScoreBridge: fetch response', {
                    ok: resp.ok,
                    status: resp.status,
                    url: resp.url,
                    contentType:
                      resp.headers && resp.headers.get
                        ? resp.headers.get('content-type')
                        : null
                  })
                  if (!resp.ok) throw new Error('fetch failed: ' + resp.status)
                  return resp.text()
                })
                .then(code => {
                  try {
                    if (
                      !document.querySelector(
                        'script[data-scorebridge-html2canvas]'
                      )
                    ) {
                      const inline = document.createElement('script')
                      inline.dataset.scorebridgeHtml2canvas = '1'
                      inline.dataset.scorebridgeHtml2canvasInline = '1'
                      inline.textContent =
                        code +
                        '\n//# sourceURL=scorebridge_html2canvas_inline.js'
                      ;(document.head || document.documentElement).appendChild(
                        inline
                      )
                      console.debug(
                        'ScoreBridge: html2canvas inline script injected'
                      )
                      try {
                        const detect = document.createElement('script')
                        detect.dataset.scorebridgeHtml2canvasDetect = '1'
                        detect.textContent =
                          "try{window.postMessage({ type: 'SCOREBRIDGE_DEBUG_HTML2CANVAS_PONG', source: 'inline_inject', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){}"
                        ;(
                          document.head || document.documentElement
                        ).appendChild(detect)
                        setTimeout(() => {
                          try {
                            detect.parentNode &&
                              detect.parentNode.removeChild(detect)
                          } catch (e) {}
                        }, 1000)
                        console.debug(
                          'ScoreBridge: injected page-context detection script (inline_inject)'
                        )
                      } catch (detEx) {
                        console.warn(
                          'ScoreBridge: inline detect injection failed',
                          detEx
                        )
                      }
                      setTimeout(
                        () =>
                          console.debug(
                            'ScoreBridge: post-inline check content-script window.html2canvas typeof:',
                            typeof window.html2canvas
                          ),
                        50
                      )
                    } else {
                      console.debug(
                        'ScoreBridge: html2canvas script already present, skipping inline injection'
                      )
                    }
                  } catch (inner) {
                    console.error(
                      'ScoreBridge: failed to inject inline html2canvas',
                      inner
                    )
                  }
                })
                .catch(fetchErr => {
                  console.error(
                    'ScoreBridge: html2canvas fetch fallback failed',
                    fetchErr
                  )
                  try {
                    chrome.runtime.sendMessage(
                      { type: 'INJECT_HTML2CANVAS' },
                      function (resp) {
                        if (resp && resp.ok) {
                          console.debug(
                            'ScoreBridge: background injection requested (sent message ok)',
                            resp
                          )
                        } else {
                          console.error(
                            'ScoreBridge: background injection failed response',
                            resp && resp.error
                          )
                        }
                      }
                    )
                  } catch (sendErr) {
                    console.error(
                      'ScoreBridge: failed to request background injection',
                      sendErr
                    )
                  }
                })
            } catch (ex) {
              console.error(
                'ScoreBridge: html2canvas loader fallback exception',
                ex
              )
            }
          }
          ;(document.head || document.documentElement).appendChild(s)
        } else {
          console.debug(
            'ScoreBridge: script tag already exists for html2canvas',
            {
              scripts: Array.from(
                document.querySelectorAll(
                  'script[data-scorebridge-html2canvas]'
                )
              ).map(s => ({
                src: s.src,
                inline: !!s.dataset.scorebridgeHtml2canvasInline
              }))
            }
          )
        }
        // inject a lightweight page-bridge that will perform captures in page context
        try {
          if (!document.querySelector('script[data-scorebridge-bridge]')) {
            const bridge = document.createElement('script')
            bridge.dataset.scorebridgeBridge = '1'
            bridge.textContent =
              "(function(){if(window.__scorebridge_bridge_installed)return;window.__scorebridge_bridge_installed=true;try{console.debug('ScoreBridge: page bridge installed',location.href);}catch(e){};window.addEventListener('message',function(ev){try{if(!ev.data)return;if(ev.data.type==='SCOREBRIDGE_PAGE_CAPTURE_REQUEST'){var nonce=ev.data.nonce,selector=ev.data.selector,options=ev.data.options;function respond(obj){try{window.postMessage(Object.assign({type:'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE',nonce:nonce},obj),'*');}catch(e){}}var el=document.querySelector(selector)||document.body;if(!window.html2canvas||typeof window.html2canvas!=='function'){respond({error:'html2canvas not available'});return;}try{window.html2canvas(el,options).then(function(canvas){try{var dataUrl=canvas.toDataURL('image/png');respond({dataUrl:dataUrl});}catch(e){respond({error:String(e)});}}).catch(function(err){respond({error:String(err)});});}catch(e){respond({error:String(e)});} } else if(ev.data.type==='SCOREBRIDGE_BRIDGE_PING'){try{window.postMessage({type:'SCOREBRIDGE_BRIDGE_PONG'},'*');}catch(e){}} }catch(e){try{window.postMessage({ type: 'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE', error:String(e) }, '*');}catch(e){} }},false);try{window.postMessage({ type: 'SCOREBRIDGE_DEBUG_HTML2CANVAS_PONG', source: 'bridge_init', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){};})();"
            ;(document.head || document.documentElement).appendChild(bridge)
            // keep the bridge element in the DOM longer while debugging
            setTimeout(() => {
              try {
                bridge.parentNode && bridge.parentNode.removeChild(bridge)
              } catch (e) {}
            }, 10000)
            console.debug('ScoreBridge: injected page-context capture bridge')
            // try a quick ping to the bridge so it can reply if installed
            try {
              setTimeout(() => {
                try {
                  window.postMessage({ type: 'SCOREBRIDGE_BRIDGE_PING' }, '*')
                  console.debug('ScoreBridge: BRIDGE_PING posted')
                } catch (e) {
                  console.warn('ScoreBridge: BRIDGE_PING failed', e)
                }
              }, 50)
            } catch (e) {
              console.warn('ScoreBridge: failed to schedule BRIDGE_PING', e)
            }
          }
        } catch (bridgeEx) {
          console.warn('ScoreBridge: bridge injection failed', bridgeEx)
        }
        const interval = 100
        let waited = 0
        const iv = setInterval(() => {
          __scorebridge_debug.ensureAttempts =
            (__scorebridge_debug.ensureAttempts || 0) + 1
          // Resolve once we've observed a page-context pong indicating html2canvas exists
          // Only resolve once we've observed a page-context pong that specifically
          // indicates the injected bridge is initialized. Other pongs (pre-inject,
          // external_onload, inline_inject) tell us html2canvas exists but do not
          // guarantee the capture bridge's message handler is ready. Requiring a
          // 'bridge_init' source avoids a race where the capture request is sent
          // before the bridge listener is installed.
          if (
            __scorebridge_debug.html2canvasPong &&
            __scorebridge_debug.html2canvasPong.exists &&
            (__scorebridge_debug.html2canvasPong.source === 'bridge_init' || __scorebridge_debug.bridgePong)
          ) {
            clearInterval(iv)
            console.debug(
              'ScoreBridge: html2canvas + bridge detected in page context via pong; resolving',
              {
                waited,
                attempts: __scorebridge_debug.ensureAttempts,
                pong: __scorebridge_debug.html2canvasPong
              }
            )
            resolve(true)
            return
          }
          if (__scorebridge_debug.ensureAttempts % 10 === 0) {
            console.debug('ScoreBridge: polling for html2canvas', {
              waited,
              attempts: __scorebridge_debug.ensureAttempts,
              scripts: Array.from(document.scripts)
                .filter(s => s.src && s.src.includes('html2canvas'))
                .map(s => s.src),
              detectedPong: __scorebridge_debug.html2canvasPong
            })
          }
          waited += interval
          if (waited >= timeoutMs) {
            clearInterval(iv)
            console.error(
              'ScoreBridge: html2canvas not available after timeout',
              {
                waited,
                attempts: __scorebridge_debug.ensureAttempts,
                detectedPong: __scorebridge_debug.html2canvasPong,
                scriptsPresent: Array.from(document.scripts)
                  .filter(s => s.src && s.src.includes('html2canvas'))
                  .map(s => s.src),
                docReadyState: document.readyState
              }
            )
            reject(new Error('html2canvas not available after timeout'))
          }
        }, interval)
      } catch (e) {
        reject(e)
      }
    })
  }

  // Request a capture to be performed in page context (page bridge will respond)
  function requestPageCapture(
    nonce,
    selector,
    options = {},
    timeoutMs = 10000
  ) {
    return new Promise((resolve, reject) => {
      try {
        console.debug(
          'CAAZ requestPageCapture------------------------------------'
        )
        console.debug(__scorebridge_pendingCaptures)
        const timer = setTimeout(() => {
          __scorebridge_pendingCaptures.delete(nonce)
          reject(new Error('page capture timed out'))
        }, timeoutMs)
        __scorebridge_pendingCaptures.set(nonce, {
          resolve: dataUrl => {
            clearTimeout(timer)
            resolve(dataUrl)
          },
          reject: err => {
            clearTimeout(timer)
            reject(err)
          }
        })
        console.debug('ScoreBridge: pending capture set', {
          nonce,
          size: __scorebridge_pendingCaptures.size
        })
        try {
          console.debug('ScoreBridge: posting PAGE_CAPTURE_REQUEST', {
            nonce,
            selector,
            options
          })
          window.postMessage(
            {
              type: 'SCOREBRIDGE_PAGE_CAPTURE_REQUEST',
              nonce,
              selector,
              options
            },
            '*'
          )
          console.debug('ScoreBridge: PAGE_CAPTURE_REQUEST posted', { nonce })
        } catch (e) {
          __scorebridge_pendingCaptures.delete(nonce)
          clearTimeout(timer)
          reject(e)
        }
      } catch (e) {
        reject(e)
      }
    })
  }

  // --- Messaging: respond to parent GET_SCORE and CAPTURE/REQUEST_IFRAME_CAPTURE requests ---
  window.addEventListener(
    'message',
    async ev => {
      try {
        if (!ev.data || typeof ev.data.type !== 'string') return
        // Handle responses from the page-context capture bridge
        if (ev.data.type === 'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE') {
          try {
            const nonce = ev.data.nonce
            const pending = __scorebridge_pendingCaptures.get(nonce)
            if (pending) {
              __scorebridge_pendingCaptures.delete(nonce)
              if (ev.data.error) pending.reject(new Error(ev.data.error))
              else pending.resolve(ev.data.dataUrl)
            } else {
              console.debug(
                'ScoreBridge: PAGE_CAPTURE_RESPONSE received but no pending capture',
                { nonce, origin: ev.origin }
              )
            }
          } catch (e) {
            console.warn('ScoreBridge: error handling PAGE_CAPTURE_RESPONSE', e)
          }
          return
        }
        // Capture debug pings from page-context detection scripts
        if (ev.data.type === 'SCOREBRIDGE_DEBUG_HTML2CANVAS_PONG') {
          console.debug('ScoreBridge: DEBUG PONG received', {
            pong: ev.data,
            origin: ev.origin,
            host: location.hostname
          })
          __scorebridge_debug.html2canvasPong = Object.assign(
            { receivedAt: Date.now() },
            ev.data,
            { origin: ev.origin }
          )
          return
        }
        // Bridge handshake pong (explicit response to BRIDGE_PING)
        if (ev.data.type === 'SCOREBRIDGE_BRIDGE_PONG') {
          try {
            console.debug('ScoreBridge: BRIDGE PONG received', { origin: ev.origin })
          } catch (e) {}
          __scorebridge_debug.bridgePong = true
          return
        }
        console.debug('ScoreBridge: message received', {
          data: ev.data,
          origin: ev.origin,
          host: location.hostname
        })
        const adapter = findAdapter(location.hostname) || null

        if (ev.data.type === 'GET_SCORE') {
          const nonce = ev.data.nonce
          console.debug('ScoreBridge: GET_SCORE request', {
            nonce,
            origin: ev.origin
          })
          const score = adapter ? adapter.readScore(document) : null
          console.debug('ScoreBridge: GET_SCORE result', { score })
          const payload = { type: 'SCORE_RESPONSE', nonce, score }
          try {
            ev.source.postMessage(payload, ev.origin || '*')
            console.debug('ScoreBridge: SCORE_RESPONSE posted', payload)
          } catch (e) {
            console.warn('ScoreBridge: postMessage failed', e)
          }
          return
        }

        if (
          ev.data.type === 'CAPTURE' ||
          ev.data.type === 'REQUEST_IFRAME_CAPTURE'
        ) {
          const nonce = ev.data.nonce
          const selector =
            ev.data.selector ||
            (adapter && typeof adapter.captureSelector === 'function'
              ? adapter.captureSelector()
              : 'body')
          console.debug('ScoreBridge: CAPTURE request', { nonce, selector })
          try {
            // Ensure vendor + bridge are present in page context
            await ensureHtml2Canvas()
            console.debug('CAAZ TEST------------------------------------')
            const options = {
              useCORS: true,
              scale: window.devicePixelRatio || 1
            }
            const dataUrl = await requestPageCapture(
              nonce,
              selector,
              options,
              15000
            )
            console.debug('CAAZ TEST 2------------------------------------')
            const payload1 = { type: 'CAPTURE_RESPONSE', nonce, dataUrl }
            const payload2 = { type: 'IFRAME_CAPTURE_RESPONSE', nonce, dataUrl }
            try {
              ev.source.postMessage(payload1, ev.origin || '*')
              ev.source.postMessage(payload2, ev.origin || '*')
              console.debug(
                'ScoreBridge: CAPTURE/IFRAME_CAPTURE responses sent',
                { nonce }
              )
            } catch (e) {
              console.warn('ScoreBridge: capture postMessage failed', e)
            }
          } catch (err) {
            console.error('ScoreBridge: capture error', err)
            try {
              ev.source.postMessage(
                { type: 'CAPTURE_RESPONSE', nonce, error: String(err) },
                ev.origin || '*'
              )
            } catch (e) {}
            try {
              ev.source.postMessage(
                { type: 'IFRAME_CAPTURE_RESPONSE', nonce, error: String(err) },
                ev.origin || '*'
              )
            } catch (e) {}
          }
          return
        }
      } catch (err) {
        console.error('ScoreBridge content-script handler error', err)
      }
    },
    false
  )

  // --- Optional: notify top frame when score changes (best-effort) ---
  ;(function watchScore() {
    const adapter = findAdapter(location.hostname)
    if (!adapter) return
    let last = null
    const el = () => document.querySelector(adapter.captureSelector()) || null
    function sendUpdate(score) {
      console.debug('ScoreBridge: sending SCORE_UPDATE', { score })
      try {
        window.top.postMessage({ type: 'SCORE_UPDATE', score }, '*')
      } catch (e) {
        console.warn('ScoreBridge: SCORE_UPDATE postMessage failed', e)
      }
    }
    const check = () => {
      const s = adapter.readScore(document)
      if (s !== last) {
        console.debug('ScoreBridge: score changed', {
          previous: last,
          current: s
        })
        last = s
        sendUpdate(s)
      }
    }
    const obs = new MutationObserver(() => {
      check()
    })
    const target = el() || document.body
    console.debug('ScoreBridge: starting MutationObserver on', target)
    try {
      obs.observe(target, {
        subtree: true,
        childList: true,
        characterData: true
      })
    } catch (e) {
      obs.observe(document.body, {
        subtree: true,
        childList: true,
        characterData: true
      })
    }
    // initial check
    setTimeout(check, 500)
    // disconnect after a reasonable timeout in PoC to avoid resource waste
    setTimeout(() => obs.disconnect(), 5 * 60 * 1000)
  })()
})()
