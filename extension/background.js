// ScoreBridge-PoC background script for tab capture requests.
'use strict'

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (!msg || typeof msg.type !== 'string') return

  if (msg.type === 'UPLOAD_IMAGE') {
    sendResponse({ ok: true })
    return true
  }
  if (msg.type === 'CAPTURE_VISIBLE_TAB') {
    try {
      if (!sender || !sender.tab) {
        sendResponse({ ok: false, error: 'no sender tab available' })
        return true
      }
      const windowId =
        typeof sender.tab.windowId !== 'undefined' ? sender.tab.windowId : null
      chrome.tabs.captureVisibleTab(
        windowId,
        { format: 'png' },
        function (dataUrl) {
          const err = chrome.runtime.lastError
          if (err || !dataUrl) {
            try {
              sendResponse({ ok: false, error: err ? err.message : 'no data' })
            } catch (e) {}
          } else {
            try {
              sendResponse({ ok: true, dataUrl })
            } catch (e) {}
          }
        }
      )
    } catch (ex) {
      try {
        sendResponse({ ok: false, error: String(ex) })
      } catch (e) {}
    }
    return true
  }
})
