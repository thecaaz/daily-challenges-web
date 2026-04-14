// ScoreBridge-PoC background script for tab capture requests and update checks.
'use strict'

// --- Capture bridge ---
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

// --- Automatic update check (self-notify) ---
// This extension cannot auto-install updates itself, but it can check a hosted
// JSON file for a newer version and notify the user with a download link.
const UPDATE_MANIFEST_URL = 'https://challenges.caaz.dev/extension/latest.json'
const UPDATE_CHECK_PERIOD_MINUTES = 60 * 6 // every 6 hours
let pendingUpdateUrl = null

function parseVersion(v) {
  return String(v).split('.').map(n => parseInt(n, 10) || 0)
}

function compareVersions(a, b) {
  const A = parseVersion(a)
  const B = parseVersion(b)
  for (let i = 0; i < Math.max(A.length, B.length); i++) {
    const av = A[i] || 0
    const bv = B[i] || 0
    if (av > bv) return 1
    if (av < bv) return -1
  }
  return 0
}

async function checkForUpdates() {
  try {
    const resp = await fetch(UPDATE_MANIFEST_URL, { cache: 'no-cache' })
    if (!resp.ok) return
    const data = await resp.json()
    if (!data || typeof data.version !== 'string') return

    const local = chrome.runtime.getManifest().version
    if (compareVersions(data.version, local) > 0) {
      // newer available
      pendingUpdateUrl = data.url || 'https://challenges.caaz.dev/extension'
      const title = `Extension update available (${data.version})`
      const message = data.notes ? String(data.notes).slice(0, 200) : 'A new version is available.'
      chrome.notifications.create('scorebridge-update-available', {
        type: 'basic',
        iconUrl: '/icons/favicon-96x96.png',
        title,
        message
      })
    }
  } catch (e) {
    // ignore transient errors
    console.debug('update check failed', e)
  }
}

chrome.notifications.onClicked.addListener(id => {
  if (id === 'scorebridge-update-available' && pendingUpdateUrl) {
    chrome.tabs.create({ url: pendingUpdateUrl })
    chrome.notifications.clear(id)
  }
})

chrome.runtime.onInstalled.addListener(() => {
  // Run an immediate check on install/update
  checkForUpdates()
  // Schedule periodic checks
  try {
    chrome.alarms.create('scorebridge-update-check', { periodInMinutes: UPDATE_CHECK_PERIOD_MINUTES })
  } catch (e) {}
})

chrome.runtime.onStartup.addListener(() => {
  // Ensure alarm exists
  try {
    chrome.alarms.create('scorebridge-update-check', { periodInMinutes: UPDATE_CHECK_PERIOD_MINUTES })
  } catch (e) {}
  checkForUpdates()
})

chrome.alarms.onAlarm.addListener(alarm => {
  if (alarm && alarm.name === 'scorebridge-update-check') checkForUpdates()
})
