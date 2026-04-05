// ScoreBridge PoC content script
(function () {
  'use strict';
  console.debug('ScoreBridge: content script loaded', { href: location.href, host: location.hostname, isTop: (window.top === window) });

  // --- Adapters (PoC built-in) ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      selectors: ['#totalText', '.score-value', '.score'],
      onInit: function (doc, ctx) {
        try {
          console.debug('TimeGuessr adapter onInit', ctx);
          const btn = doc.querySelector('#tg-embed-continue');
          if (btn && typeof btn.click === 'function') btn.click();
        } catch (e) { console.warn('TimeGuessr onInit failed', e); }
      },
      readScore: function (doc) {
        for (const s of this.selectors) {
          const el = doc.querySelector(s);
          if (!el) continue;
          const text = (el.textContent || '').replace(/[^0-9.\-]/g, '').trim();
          if (!text) continue;
          const n = Number(text);
          return Number.isNaN(n) ? text : n;
        }
        return null;
      },
      captureSelector: function () { return this.selectors[0]; }
    }
  ];

  function findAdapter(host) { return ADAPTERS.find(a => a.match && a.match(host)); }

  // Call adapter onInit if present
  (function runAdapterInit() {
    try {
      const adapter = findAdapter(location.hostname);
      if (!adapter || typeof adapter.onInit !== 'function') return;
      const ctx = { host: location.hostname, href: location.href, inIframe: window.top !== window };
      const res = adapter.onInit(document, ctx);
      if (res && typeof res.then === 'function') res.catch(err => console.error('ScoreBridge: adapter onInit async error', err));
    } catch (err) {
      console.error('ScoreBridge: adapter onInit error', err);
    }
  })();

  // --- html2canvas helper: load vendor loader and wait for html2canvas to be available ---
  function ensureHtml2Canvas(timeoutMs = 5000) {
    if (window.html2canvas && typeof window.html2canvas === 'function') return Promise.resolve(window.html2canvas);
    const vendorUrl = chrome.runtime.getURL('vendor/html2canvas.min.js');
    return new Promise((resolve, reject) => {
      try {
        if (!document.querySelector('script[data-scorebridge-html2canvas]')) {
          const s = document.createElement('script');
          s.dataset.scorebridgeHtml2canvas = '1';
          s.src = vendorUrl;
          s.async = true;
          s.onload = () => { console.debug('ScoreBridge: vendor loader script loaded'); };
          s.onerror = (e) => {
            console.warn('ScoreBridge: html2canvas loader error, attempting fallback fetch', e);
            try {
              // Attempt to fetch the library and inject it inline as a fallback
              fetch(vendorUrl).then(resp => {
                if (!resp.ok) throw new Error('fetch failed: ' + resp.status);
                return resp.text();
              }).then(code => {
                try {
                  if (!document.querySelector('script[data-scorebridge-html2canvas]')) {
                    const inline = document.createElement('script');
                    inline.dataset.scorebridgeHtml2canvas = '1';
                    inline.dataset.scorebridgeHtml2canvasInline = '1';
                    // include a sourceURL to make debugging easier
                    inline.textContent = code + '\n//# sourceURL=scorebridge_html2canvas_inline.js';
                    (document.head || document.documentElement).appendChild(inline);
                    console.debug('ScoreBridge: html2canvas inline script injected');
                  } else {
                    console.debug('ScoreBridge: html2canvas script already present, skipping inline injection');
                  }
                } catch (inner) {
                  console.error('ScoreBridge: failed to inject inline html2canvas', inner);
                }
              }).catch(fetchErr => {
                    console.error('ScoreBridge: html2canvas fetch fallback failed', fetchErr);
                    try {
                      // Ask background page to inject the vendor file (more privileged injection)
                      chrome.runtime.sendMessage({ type: 'INJECT_HTML2CANVAS' }, function (resp) {
                        if (resp && resp.ok) {
                          console.debug('ScoreBridge: background injection requested');
                        } else {
                          console.error('ScoreBridge: background injection failed', resp && resp.error);
                        }
                      });
                    } catch (sendErr) {
                      console.error('ScoreBridge: failed to request background injection', sendErr);
                    }
                  });
            } catch (ex) {
              console.error('ScoreBridge: html2canvas loader fallback exception', ex);
            }
          };
          (document.head || document.documentElement).appendChild(s);
        }
        const interval = 100;
        let waited = 0;
        const iv = setInterval(() => {
          if (window.html2canvas && typeof window.html2canvas === 'function') {
            clearInterval(iv);
            resolve(window.html2canvas);
            return;
          }
          waited += interval;
          if (waited >= timeoutMs) {
            clearInterval(iv);
            reject(new Error('html2canvas not available after timeout'));
          }
        }, interval);
      } catch (e) { reject(e); }
    });
  }

  // --- Messaging: respond to parent GET_SCORE and CAPTURE/REQUEST_IFRAME_CAPTURE requests ---
  window.addEventListener('message', async ev => {
    try {
      if (!ev.data || typeof ev.data.type !== 'string') return;
      console.debug('ScoreBridge: message received', { data: ev.data, origin: ev.origin, host: location.hostname });
      const adapter = findAdapter(location.hostname) || null;

      if (ev.data.type === 'GET_SCORE') {
        const nonce = ev.data.nonce;
        console.debug('ScoreBridge: GET_SCORE request', { nonce, origin: ev.origin });
        const score = adapter ? adapter.readScore(document) : null;
        console.debug('ScoreBridge: GET_SCORE result', { score });
        const payload = { type: 'SCORE_RESPONSE', nonce, score };
        try { ev.source.postMessage(payload, ev.origin || '*'); console.debug('ScoreBridge: SCORE_RESPONSE posted', payload); } catch (e) { console.warn('ScoreBridge: postMessage failed', e); }
        return;
      }

      if (ev.data.type === 'CAPTURE' || ev.data.type === 'REQUEST_IFRAME_CAPTURE') {
        const nonce = ev.data.nonce;
        const selector = ev.data.selector || (adapter && typeof adapter.captureSelector === 'function' ? adapter.captureSelector() : 'body');
        console.debug('ScoreBridge: CAPTURE request', { nonce, selector });
        try {
          const h2c = await ensureHtml2Canvas();
          const el = document.querySelector(selector) || document.body;
          const options = { useCORS: true, scale: window.devicePixelRatio || 1 };
          const canvas = await h2c(el, options);
          const dataUrl = canvas.toDataURL('image/png');
          const payload1 = { type: 'CAPTURE_RESPONSE', nonce, dataUrl };
          const payload2 = { type: 'IFRAME_CAPTURE_RESPONSE', nonce, dataUrl };
          try {
            ev.source.postMessage(payload1, ev.origin || '*');
            ev.source.postMessage(payload2, ev.origin || '*');
            console.debug('ScoreBridge: CAPTURE/IFRAME_CAPTURE responses sent', { nonce });
          } catch (e) { console.warn('ScoreBridge: capture postMessage failed', e); }
        } catch (err) {
          console.error('ScoreBridge: capture error', err);
          try { ev.source.postMessage({ type: 'CAPTURE_RESPONSE', nonce, error: String(err) }, ev.origin || '*'); } catch (e) {}
          try { ev.source.postMessage({ type: 'IFRAME_CAPTURE_RESPONSE', nonce, error: String(err) }, ev.origin || '*'); } catch (e) {}
        }
        return;
      }

    } catch (err) {
      console.error('ScoreBridge content-script handler error', err);
    }
  }, false);

  // --- Optional: notify top frame when score changes (best-effort) ---
  (function watchScore() {
    const adapter = findAdapter(location.hostname);
    if (!adapter) return;
    let last = null;
    const el = () => document.querySelector(adapter.captureSelector()) || null;
    function sendUpdate(score) {
      console.debug('ScoreBridge: sending SCORE_UPDATE', { score });
      try { window.top.postMessage({ type: 'SCORE_UPDATE', score }, '*'); } catch (e) { console.warn('ScoreBridge: SCORE_UPDATE postMessage failed', e); }
    }
    const check = () => {
      const s = adapter.readScore(document);
      if (s !== last) { console.debug('ScoreBridge: score changed', { previous: last, current: s }); last = s; sendUpdate(s); }
    };
    const obs = new MutationObserver(() => { check(); });
    const target = el() || document.body;
    console.debug('ScoreBridge: starting MutationObserver on', target);
    try { obs.observe(target, { subtree: true, childList: true, characterData: true }); } catch (e) { obs.observe(document.body, { subtree: true, childList: true, characterData: true }); }
    // initial check
    setTimeout(check, 500);
    // disconnect after a reasonable timeout in PoC to avoid resource waste
    setTimeout(() => obs.disconnect(), 5 * 60 * 1000);
  })();

})();
