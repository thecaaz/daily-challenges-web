// ScoreBridge PoC background service worker
'use strict';

console.log('ScoreBridge: background script started');

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (!msg || typeof msg.type !== 'string') {
    console.debug('ScoreBridge: background received non-message', msg);
    return;
  }

  console.debug('ScoreBridge: background message', { type: msg.type, sender });

  if (msg.type === 'UPLOAD_IMAGE') {
    // PoC: simply log. Replace with your backend endpoint and proper auth in production.
    console.log('ScoreBridge: UPLOAD_IMAGE received from', sender?.tab?.url || sender?.url, { size: msg.dataUrl ? msg.dataUrl.length : 0 });
    // Example: forward to backend (uncomment and configure for real use)
    // fetch('https://your.backend/score-image', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({image: msg.dataUrl, pageUrl: sender?.tab?.url}) })
    //   .then(r => sendResponse({ ok: true })).catch(e => sendResponse({ ok: false, error: String(e) }));
    sendResponse({ ok: true });
    return true;
  }
});
