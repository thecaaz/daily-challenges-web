// ScoreBridge PoC content script
(function () {
  'use strict';
  console.debug('ScoreBridge: content script loaded', { href: location.href, host: location.hostname, isTop: (window.top === window) });

  // --- Adapters (PoC: embedded) ---
  const ADAPTERS = [
    {
      name: 'TimeGuessr',
      match: host => /timeguessr/.test(host) || host.includes('timeguessr'),
      // selectors to try for readScore / capture element
      selectors: ['#totalText'],
      readScore: function (doc) {
        for (const s of this.selectors) {
          const el = doc.querySelector(s);
          if (!el) continue;
          const text = el.textContent.replace(/[^0-9.\-]/g, '').trim();
          if (text.length === 0) continue;
          const n = Number(text);
          return Number.isNaN(n) ? text : n;
        }
        return null;
      },
      captureSelector: function () { return this.selectors[0]; }
    }
  ];

  function findAdapter(host) {
    return ADAPTERS.find(a => a.match(host));
  }

  // Capture functionality removed for PoC focus on score reading.

  // --- Messaging: respond to parent GET_SCORE requests ---
  window.addEventListener('message', ev => {
    try {
      if (!ev.data || typeof ev.data.type !== 'string') return;
      console.debug('ScoreBridge: message received', { data: ev.data, origin: ev.origin, host: location.hostname });
      const adapter = findAdapter(location.hostname) || null;
      console.debug('ScoreBridge: adapter', adapter ? adapter.name : null);

      if (ev.data.type === 'GET_SCORE') {
        const nonce = ev.data.nonce;
        console.debug('ScoreBridge: GET_SCORE request', { nonce, origin: ev.origin });
        const score = adapter ? adapter.readScore(document) : null;
        console.debug('ScoreBridge: GET_SCORE result', { score });
        const payload = { type: 'SCORE_RESPONSE', nonce, score };
        try { ev.source.postMessage(payload, ev.origin || '*'); console.debug('ScoreBridge: SCORE_RESPONSE posted', payload); } catch (e) { console.warn('ScoreBridge: postMessage failed', e); }
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
    obs.observe(target, { subtree: true, childList: true, characterData: true });
    // initial check
    setTimeout(check, 500);
    // disconnect after a reasonable timeout in PoC to avoid resource waste
    setTimeout(() => obs.disconnect(), 5 * 60 * 1000);
  })();

})();
