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
            bridge.textContent = `(function(){
  if(window.__scorebridge_bridge_installed) return;
  window.__scorebridge_bridge_installed = true;

  function _sleep(ms){ return new Promise(r => setTimeout(r, ms)); }

  function _tryToDataURL(canvas){
    return new Promise(async (resolve, reject) => {
      try {
        // fast path: toDataURL
        try {
          const url = canvas.toDataURL('image/png');
          return resolve(url);
        } catch(e){}

        // try drawImage onto a 2D canvas
        try {
          const out = document.createElement('canvas');
          out.width = canvas.width || Math.max(1, Math.round(canvas.clientWidth));
          out.height = canvas.height || Math.max(1, Math.round(canvas.clientHeight));
          const ctx = out.getContext('2d');
          ctx.drawImage(canvas, 0, 0, out.width, out.height);
          return resolve(out.toDataURL('image/png'));
        } catch(e){}

        // try WebGL readPixels fallback
        try {
          const gl = canvas.getContext('webgl') || canvas.getContext('webgl2') || canvas.getContext('experimental-webgl');
          if (!gl) throw new Error('no-webgl-context');
          const width = canvas.width || Math.max(1, Math.round(canvas.clientWidth));
          const height = canvas.height || Math.max(1, Math.round(canvas.clientHeight));
          const pixels = new Uint8Array(width * height * 4);
          gl.readPixels(0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
          const out = document.createElement('canvas');
          out.width = width; out.height = height;
          const ctx = out.getContext('2d');
          const imageData = ctx.createImageData(width, height);
          for (let y = 0; y < height; y++){
            const srcRow = height - 1 - y;
            for (let x = 0; x < width; x++){
              const srcIndex = (srcRow * width + x) * 4;
              const dstIndex = (y * width + x) * 4;
              imageData.data[dstIndex] = pixels[srcIndex];
              imageData.data[dstIndex+1] = pixels[srcIndex+1];
              imageData.data[dstIndex+2] = pixels[srcIndex+2];
              imageData.data[dstIndex+3] = pixels[srcIndex+3];
            }
          }
          ctx.putImageData(imageData, 0, 0);
          return resolve(out.toDataURL('image/png'));
        } catch(e){}

        reject(new Error('all-capture-methods-failed'));
      } catch (ex) { reject(ex); }
    });
  }

  function _isDataUrlBlank(dataUrl){
    return new Promise((resolve) => {
      try {
        const img = new Image();
        img.onload = function(){
          try {
            const w = Math.max(1, Math.floor(img.width / 10));
            const h = Math.max(1, Math.floor(img.height / 10));
            const c = document.createElement('canvas');
            c.width = w; c.height = h;
            const ctx = c.getContext('2d');
            ctx.drawImage(img, 0, 0, w, h);
            try {
              const d = ctx.getImageData(0,0,w,h).data;
              let s = 0;
              for (let i = 0; i < d.length; i += 4){ s += d[i] + d[i+1] + d[i+2] + d[i+3]; if (s > 0) break; }
              resolve(s === 0);
            } catch (e) { resolve(false); }
          } catch (e) { resolve(false); }
        };
        img.onerror = function(){ resolve(true); };
        img.src = dataUrl;
      } catch (e) { resolve(true); }
    });
  }

  async function captureCanvasWithRetries(canvas, tries, delay){
    let last = null;
    for (let i = 0; i < tries; i++){
      try {
        const dataUrl = await _tryToDataURL(canvas);
        if (dataUrl){
          const blank = await _isDataUrlBlank(dataUrl);
          if (!blank) return dataUrl;
          last = dataUrl;
        }
      } catch (e) {}
      // let the page render more frames
      await new Promise(r => requestAnimationFrame(r));
      if (delay) await _sleep(delay);
    }
    return last;
  }

  window.addEventListener('message', async function (ev) {
    try {
      if (!ev.data) return;
      if (ev.data.type === 'SCOREBRIDGE_PAGE_CAPTURE_REQUEST'){
        const nonce = ev.data.nonce;
        const selector = ev.data.selector;
        const options = ev.data.options || {};
        function respond(obj){ try { window.postMessage(Object.assign({ type: 'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE', nonce: nonce }, obj), '*'); } catch(e) {} }
        const el = document.querySelector(selector) || document.body;
        try {
          const canvases = el && el.nodeName === 'CANVAS' ? [el] : (el ? Array.prototype.slice.call(el.querySelectorAll('canvas')) : []);
          for (const c of canvases) { try { c.dataset.scorebridgeCanvasId = c.dataset.scorebridgeCanvasId || Math.random().toString(36).slice(2); } catch (e) {} }

          const snapshots = {};
          if (canvases.length > 0) {
            await Promise.all(canvases.map(async (c) => {
              try {
                const url = await captureCanvasWithRetries(c, 6, 50);
                if (url) snapshots[c.dataset.scorebridgeCanvasId] = url;
              } catch (e) {}
            }));

            if (Object.keys(snapshots).length > 0) {
              try {
                const rect = el.getBoundingClientRect();
                const out = document.createElement('canvas');
                out.width = Math.max(1, Math.ceil(rect.width));
                out.height = Math.max(1, Math.ceil(rect.height));
                const ctx = out.getContext('2d');
                for (const c of canvases) {
                  try {
                    const r = c.getBoundingClientRect();
                    const x = Math.round(r.left - rect.left);
                    const y = Math.round(r.top - rect.top);
                    const id = c.dataset.scorebridgeCanvasId;
                    const src = snapshots[id];
                    if (src) {
                      const img = new Image();
                      img.src = src;
                      await new Promise((res, rej) => { img.onload = res; img.onerror = rej; });
                      ctx.drawImage(img, x, y, Math.round(r.width), Math.round(r.height));
                    } else {
                      try { ctx.drawImage(c, x, y, Math.round(r.width), Math.round(r.height)); } catch (e2) {}
                    }
                  } catch (e) {}
                }
                const final = out.toDataURL('image/png');
                respond({ dataUrl: final, fallback: 'composite-snapshots' });
                return;
              } catch (e) {}
            }
          }
        } catch (e) {}

        if (!window.html2canvas || typeof window.html2canvas !== 'function') { respond({ error: 'html2canvas not available' }); return; }

        try {
          const allCanvases = el && el.nodeName === 'CANVAS' ? [el] : (el ? Array.prototype.slice.call(el.querySelectorAll('canvas')) : []);
          const snapshots2 = {};
          await Promise.all(allCanvases.map(async (c) => {
            try {
              const id = c.dataset.scorebridgeCanvasId || (c.dataset.scorebridgeCanvasId = Math.random().toString(36).slice(2));
              const url = await captureCanvasWithRetries(c, 6, 50);
              if (url) snapshots2[id] = url;
            } catch (e) {}
          }));

          const opts = Object.assign({}, options, { onclone: function (clonedDoc) {
            try {
              const nodes = clonedDoc.querySelectorAll('canvas[data-scorebridge-canvas-id]');
              nodes.forEach(function (n) {
                try {
                  const id = n.getAttribute('data-scorebridge-canvas-id');
                  const src = snapshots2[id];
                  if (src) {
                    const img = clonedDoc.createElement('img');
                    img.src = src;
                    img.style.width = n.style.width || (n.width ? (n.width + 'px') : '');
                    img.style.height = n.style.height || (n.height ? (n.height + 'px') : '');
                    n.parentNode.replaceChild(img, n);
                  }
                } catch (e) {}
              });
            } catch (e) {}
          } });
          const canvas = await window.html2canvas(el, opts);
          const dataUrl = canvas.toDataURL('image/png');
          respond({ dataUrl: dataUrl });
          return;
        } catch (err) {
          respond({ error: String(err) });
          return;
        }
      } else if (ev.data.type === 'SCOREBRIDGE_BRIDGE_PING') {
        try { window.postMessage({ type: 'SCOREBRIDGE_BRIDGE_PONG' }, '*'); } catch (e) {}
      }
    } catch (e) {
      try { window.postMessage({ type: 'SCOREBRIDGE_PAGE_CAPTURE_RESPONSE', error: String(e) }, '*'); } catch (o) {}
    }
  }, false);

  try { window.postMessage({ type: 'SCOREBRIDGE_HTML2CANVAS_PONG', source: 'bridge_init', exists: typeof html2canvas !== 'undefined', typeOf: typeof html2canvas }, '*'); } catch (e) {}
})();`
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

        // Parent -> child asks the child to request a visible-tab capture from top
        if (ev.data.type === 'REQUEST_VISIBLE_TAB_FROM_PARENT') {
          try {
            // Only forward from within a frame (child)
            if (window.top === window) return
            const selector = ev.data.selector || null
            const nonce = ev.data.nonce
            let el = null
            try { el = selector ? document.querySelector(selector) : null } catch (e) { el = null }
            let rect
            if (el) {
              const r = el.getBoundingClientRect()
              rect = { left: r.left, top: r.top, width: r.width, height: r.height }
            } else {
              const w = document.documentElement.clientWidth || window.innerWidth || 0
              const h = document.documentElement.clientHeight || window.innerHeight || 0
              rect = { left: 0, top: 0, width: w, height: h }
            }
            try { window.top.postMessage({ type: 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE', nonce, rect }, '*') } catch (e) {}
          } catch (e) {}
          return
        }

        // Visible-tab capture request from an inner frame: only the top frame should handle
        if (ev.data.type === 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE') {
          try {
            if (window.top !== window) return
            const nonce = ev.data.nonce
            const childRect = ev.data.rect || { left: 0, top: 0, width: 0, height: 0 }
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
              try { ev.source && ev.source.postMessage && ev.source.postMessage({ type: 'CAPTURE_RESPONSE', nonce, error: 'iframe element not found' }, ev.origin || '*') } catch (e) {}
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
              chrome.runtime.sendMessage({ type: 'CAPTURE_VISIBLE_TAB', rect: abs, dpr }, resp => {
                try {
                  if (!resp || !resp.ok || !resp.dataUrl) {
                    const payloadErr = { type: 'CAPTURE_RESPONSE', nonce, error: resp && resp.error ? resp.error : 'capture failed' }
                    try { window.postMessage(payloadErr, '*') } catch (e) {}
                    try { iframeEl.contentWindow.postMessage(payloadErr, ev.origin || '*') } catch (e) {}
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
                      const payloadOk = { type: 'CAPTURE_RESPONSE', nonce, dataUrl: final }
                      try { window.postMessage(payloadOk, '*') } catch (e) {}
                      try { iframeEl.contentWindow.postMessage(payloadOk, ev.origin || '*') } catch (e) {}
                    } catch (err) {
                      const payloadErr2 = { type: 'CAPTURE_RESPONSE', nonce, error: String(err) }
                      try { window.postMessage(payloadErr2, '*') } catch (e) {}
                      try { iframeEl.contentWindow.postMessage(payloadErr2, ev.origin || '*') } catch (e) {}
                    }
                  }
                  img.onerror = function () { try { window.postMessage({ type: 'CAPTURE_RESPONSE', nonce, error: 'image load error' }, '*') } catch (e) {} ; try { iframeEl.contentWindow.postMessage({ type: 'CAPTURE_RESPONSE', nonce, error: 'image load error' }, ev.origin || '*') } catch (e) {} }
                  img.src = resp.dataUrl
                } catch (e) {
                  const payloadErr3 = { type: 'CAPTURE_RESPONSE', nonce, error: String(e) }
                  try { window.postMessage(payloadErr3, '*') } catch (o) {}
                  try { iframeEl.contentWindow.postMessage(payloadErr3, ev.origin || '*') } catch (o) {}
                }
              })
            } catch (e) {
              const payloadErr4 = { type: 'CAPTURE_RESPONSE', nonce, error: String(e) }
              try { window.postMessage(payloadErr4, '*') } catch (o) {}
              try { iframeEl.contentWindow.postMessage(payloadErr4, ev.origin || '*') } catch (o) {}
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
