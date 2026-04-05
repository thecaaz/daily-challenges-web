// ScoreBridge PoC content script
;(function () {
  'use strict'
  // runtime bridge/html2canvas detection state
  let html2canvasPong = null
  let ensureAttempts = 0
  let bridgePong = false
  const __scorebridge_pendingCaptures = new Map()

  // --- Adapters (PoC built-in) ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      selectors: ['#totalText', '.score-value', '.score'],
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

  // --- html2canvas helper: load vendor loader and wait for html2canvas to be available ---
  function ensureHtml2Canvas(timeoutMs = 5000) {
  
    // Note: html2canvas will live in the page context (not the isolated content-script
    // world). We cannot access it directly via `window.html2canvas` here. Instead we
    // rely on the page-context detection pongs and a small injected bridge that will
    // execute html2canvas in the page and return results via postMessage.
    if (window.html2canvas && typeof window.html2canvas === 'function') {
      // html2canvas already present in content-script context
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
              "try{window.postMessage({ type: 'SCOREBRIDGE_HTML2CANVAS_PONG', source: 'detect_preinject', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){}"
            ;(document.head || document.documentElement).appendChild(detect)
            setTimeout(() => {
              try {
                detect.parentNode && detect.parentNode.removeChild(detect)
              } catch (e) {}
            }, 1000)
          }
        } catch (detEx) {}

        if (!document.querySelector('script[data-scorebridge-html2canvas]')) {
          // injecting external html2canvas script
          const s = document.createElement('script')
          s.dataset.scorebridgeHtml2canvas = '1'
          s.src = vendorUrl
          s.async = true
            s.onload = () => {
            try {
              const detect = document.createElement('script')
              detect.dataset.scorebridgeHtml2canvasDetect = '1'
              detect.textContent =
                "try{window.postMessage({ type: 'SCOREBRIDGE_HTML2CANVAS_PONG', source: 'external_onload', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){}"
              ;(document.head || document.documentElement).appendChild(detect)
              setTimeout(() => {
                try {
                  detect.parentNode && detect.parentNode.removeChild(detect)
                } catch (e) {}
              }, 1000)
            } catch (detEx) {}
          }
          s.onerror = e => {
            try {
              fetch(vendorUrl)
                .then(resp => {
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
                      try {
                        const detect = document.createElement('script')
                        detect.dataset.scorebridgeHtml2canvasDetect = '1'
                        detect.textContent =
                          "try{window.postMessage({ type: 'SCOREBRIDGE_HTML2CANVAS_PONG', source: 'inline_inject', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){}"
                        ;(
                          document.head || document.documentElement
                        ).appendChild(detect)
                        setTimeout(() => {
                          try {
                            detect.parentNode &&
                              detect.parentNode.removeChild(detect)
                          } catch (e) {}
                        }, 1000)
                      } catch (detEx) {}
                    }
                  } catch (inner) {}
                })
                .catch(fetchErr => {
                  try {
                    chrome.runtime.sendMessage(
                      { type: 'INJECT_HTML2CANVAS' },
                      function (resp) {
                        // ignore response
                      }
                    )
                  } catch (sendErr) {}
                })
            } catch (ex) {}
          }
          ;(document.head || document.documentElement).appendChild(s)
        } else {
          // script tag already exists for html2canvas
        }
        // inject a lightweight page-bridge that will perform captures in page context
        try {
          if (!document.querySelector('script[data-scorebridge-bridge]')) {
            const bridge = document.createElement('script')
            bridge.dataset.scorebridgeBridge = '1'
            bridge.textContent = "(function(){if(window.__scorebridge_bridge_installed)return;window.__scorebridge_bridge_installed=true;window.addEventListener('message',function(ev){try{if(!ev.data)return;if(ev.data.type==='SCOREBRIDGE_PAGE_CAPTURE_REQUEST'){var nonce=ev.data.nonce,selector=ev.data.selector,options=ev.data.options;function respond(obj){try{window.postMessage(Object.assign({type:'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE',nonce:nonce},obj),'*');}catch(e){}}var el=document.querySelector(selector)||document.body;try{var canvases=[];if(el&&el.nodeName==='CANVAS'){canvases=[el];}else if(el){canvases=Array.prototype.slice.call(el.querySelectorAll('canvas'));}if(canvases.length===1){try{var dataUrl=canvases[0].toDataURL('image/png');respond({dataUrl:dataUrl,fallback:'canvas-toDataURL'});return;}catch(e){} } else if(canvases.length>1){try{var elRect=el.getBoundingClientRect();var out=document.createElement('canvas');out.width=Math.ceil(elRect.width);out.height=Math.ceil(elRect.height);var ctx=out.getContext('2d');canvases.forEach(function(c){try{var r=c.getBoundingClientRect();var x=Math.round(r.left-elRect.left);var y=Math.round(r.top-elRect.top);ctx.drawImage(c,x,y,Math.round(r.width),Math.round(r.height));}catch(e2){try{var tmpUrl=c.toDataURL('image/png');var img=new Image();img.src=tmpUrl;ctx.drawImage(img,x,y);}catch(e3){}}});var final=out.toDataURL('image/png');respond({dataUrl:final,fallback:'composite-canvases'});return;}catch(e){}}}catch(e){}if(!window.html2canvas||typeof window.html2canvas!=='function'){respond({error:'html2canvas not available'});return;}try{window.html2canvas(el,options).then(function(canvas){try{var dataUrl=canvas.toDataURL('image/png');respond({dataUrl:dataUrl});}catch(e){respond({error:String(e)});}}).catch(function(err){respond({error:String(err)});});}catch(e){respond({error:String(e)});} } else if(ev.data.type==='SCOREBRIDGE_BRIDGE_PING'){try{window.postMessage({type:'SCOREBRIDGE_BRIDGE_PONG'},'*');}catch(e){}} }catch(e){try{window.postMessage({ type: 'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE', error:String(e) }, '*');}catch(e){} }},false);try{window.postMessage({ type: 'SCOREBRIDGE_HTML2CANVAS_PONG', source: 'bridge_init', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*');}catch(e){};})();"
            ;(document.head || document.documentElement).appendChild(bridge)
            setTimeout(() => {
              try {
                bridge.parentNode && bridge.parentNode.removeChild(bridge)
              } catch (e) {}
            }, 10000)
            try {
              setTimeout(() => {
                try {
                  window.postMessage({ type: 'SCOREBRIDGE_BRIDGE_PING' }, '*')
                } catch (e) {}
              }, 50)
            } catch (e) {}
          }
        } catch (bridgeEx) {}
        const interval = 100
        let waited = 0
        const iv = setInterval(() => {
          ensureAttempts = (ensureAttempts || 0) + 1
          // Resolve once we've observed a page-context pong indicating html2canvas exists
          // Only resolve once we've observed a page-context pong that specifically
          // indicates the injected bridge is initialized. Other pongs (pre-inject,
          // external_onload, inline_inject) tell us html2canvas exists but do not
          // guarantee the capture bridge's message handler is ready. Requiring a
          // 'bridge_init' source avoids a race where the capture request is sent
          // before the bridge listener is installed.
          if (
            html2canvasPong &&
            html2canvasPong.exists &&
            (html2canvasPong.source === 'bridge_init' || bridgePong)
          ) {
            clearInterval(iv)
            resolve(true)
            return
          }
          if (ensureAttempts % 10 === 0) {
          
          }
          waited += interval
          if (waited >= timeoutMs) {
            clearInterval(iv)
            // html2canvas not available after timeout
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
        // pending capture set
        try {
          window.postMessage(
            {
              type: 'SCOREBRIDGE_PAGE_CAPTURE_REQUEST',
              nonce,
              selector,
              options
            },
            '*'
          )
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
            }
          } catch (e) {
            // ignore
          }
          return
        }
        // Capture pings from page-context detection scripts
        if (ev.data.type === 'SCOREBRIDGE_HTML2CANVAS_PONG') {
          html2canvasPong = Object.assign(
            { receivedAt: Date.now() },
            ev.data,
            { origin: ev.origin }
          )
          return
        }
        // Bridge handshake pong (explicit response to BRIDGE_PING)
        if (ev.data.type === 'SCOREBRIDGE_BRIDGE_PONG') {
          bridgePong = true
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
          } catch (e) {}
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
          // CAPTURE request
          try {
            // Ensure vendor + bridge are present in page context
            await ensureHtml2Canvas()
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
            const payload1 = { type: 'CAPTURE_RESPONSE', nonce, dataUrl }
            const payload2 = { type: 'IFRAME_CAPTURE_RESPONSE', nonce, dataUrl }
            try {
              ev.source.postMessage(payload1, ev.origin || '*')
              ev.source.postMessage(payload2, ev.origin || '*')
            } catch (e) {}
          } catch (err) {
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
      try {
        window.top.postMessage({ type: 'SCORE_UPDATE', score }, '*')
      } catch (e) {}
    }
    const check = () => {
      const s = adapter.readScore(document)
      if (s !== last) {
        last = s
        sendUpdate(s)
      }
    }
    const obs = new MutationObserver(() => {
      check()
    })
    const target = el() || document.body
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
