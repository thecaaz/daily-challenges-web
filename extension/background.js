// ScoreBridge PoC background service worker
'use strict';


chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (!msg || typeof msg.type !== 'string') return;

  if (msg.type === 'UPLOAD_IMAGE') {
    // PoC: simply acknowledge. Replace with your backend endpoint and proper auth in production.
    // Example: forward to backend (uncomment and configure for real use)
    // fetch('https://your.backend/score-image', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({image: msg.dataUrl, pageUrl: sender?.tab?.url}) })
    //   .then(r => sendResponse({ ok: true })).catch(e => sendResponse({ ok: false, error: String(e) }));
    sendResponse({ ok: true });
    return true;
  }
  if (msg.type === 'INJECT_HTML2CANVAS') {
    // Attempt to inject the packaged vendor/html2canvas.min.js into the sender's tab/frame
    try {
      if (!sender || !sender.tab || typeof sender.tab.id === 'undefined') {
        sendResponse({ ok: false, error: 'no sender tab available' });
        return true;
      }
      const tabId = sender.tab.id;
      const frameId = sender.frameId;
      chrome.tabs.executeScript(tabId, { file: 'vendor/html2canvas.min.js', frameId, runAt: 'document_idle' }, () => {
        const err = chrome.runtime.lastError;
        if (err) {
          try { sendResponse({ ok: false, error: err.message }); } catch (e) {}
        } else {
          try { sendResponse({ ok: true }); } catch (e) {}
        }
      });
    } catch (ex) {
      try { sendResponse({ ok: false, error: String(ex) }); } catch (e) {}
    }
    return true;
  }
  if (msg.type === 'CAPTURE_VISIBLE_TAB') {
    try {
      if (!sender || !sender.tab) {
        sendResponse({ ok: false, error: 'no sender tab available' });
        return true;
      }
      const windowId = typeof sender.tab.windowId !== 'undefined' ? sender.tab.windowId : null;
      chrome.tabs.captureVisibleTab(windowId, { format: 'png' }, function (dataUrl) {
        const err = chrome.runtime.lastError;
        if (err || !dataUrl) {
          try { sendResponse({ ok: false, error: err ? err.message : 'no data' }); } catch (e) {}
        } else {
          try { sendResponse({ ok: true, dataUrl }); } catch (e) {}
        }
      });
    } catch (ex) {
      try { sendResponse({ ok: false, error: String(ex) }); } catch (e) {}
    }
    return true;
  }
});
