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
  if (msg.type === 'INJECT_HTML2CANVAS') {
    // Attempt to inject the packaged vendor/html2canvas.min.js into the sender's tab/frame
    try {
      console.debug('ScoreBridge: INJECT_HTML2CANVAS handler invoked', { sender });
      if (!sender || !sender.tab || typeof sender.tab.id === 'undefined') {
        sendResponse({ ok: false, error: 'no sender tab available' });
        return true;
      }
      const tabId = sender.tab.id;
      const frameId = sender.frameId;
      console.debug('ScoreBridge: executing chrome.tabs.executeScript', { tabId, frameId, file: 'vendor/html2canvas.min.js' });
      chrome.tabs.executeScript(tabId, { file: 'vendor/html2canvas.min.js', frameId, runAt: 'document_idle' }, () => {
        const err = chrome.runtime.lastError;
        console.debug('ScoreBridge: executeScript callback, chrome.runtime.lastError', err);
        if (err) {
          console.error('ScoreBridge: chrome.tabs.executeScript error', err);
          try { sendResponse({ ok: false, error: err.message }); } catch (e) { console.error('ScoreBridge: sendResponse failed', e); }
        } else {
          console.debug('ScoreBridge: injected vendor/html2canvas.min.js via tabs.executeScript');
          try { sendResponse({ ok: true }); console.debug('ScoreBridge: sendResponse(ok:true) called'); } catch (e) { console.error('ScoreBridge: sendResponse failed', e); }
        }
      });
    } catch (ex) {
      console.error('ScoreBridge: INJECT_HTML2CANVAS handler exception', ex);
      sendResponse({ ok: false, error: String(ex) });
    }
    return true;
  }
});
