# ScoreBridge-PoC - README

Quick summary
-------------

This directory contains a Manifest V2 browser extension that lets a parent page talk to embedded game pages with `window.postMessage`. The extension currently supports four main actions:

- detect whether a built-in adapter exists for a URL
- list the built-in adapters
- read a score from a supported game page
- capture the visible tab from the top frame and return the cropped iframe region

Current built-in adapters:

- `TimeGuessr`: reads `#totalText` or `#totalScoreBreakdownText`, and clicks `#breakdownButton` first when present
- `MapTap`: reads `#ui_score`

Contents
--------

- `extension/` - extension source (`manifest.json`, `content-script.js`, `background.js`)
- `frontend/test-harness.html` - manual harness that embeds a game page and exercises the extension message flow

Install and run (Firefox)
-------------------------

Option A - Load as a temporary add-on:

1. Open Firefox.
2. Navigate to `about:debugging#/runtime/this-firefox`.
3. Click **Load Temporary Add-on**.
4. Select `extension/manifest.json`.

Notes:

- This load is temporary and is cleared when Firefox restarts.
- Reload any already-open target pages after loading the extension so the content script is injected.

Option B - Use `web-ext` for iterative local work:

```powershell
npm install --global web-ext
npx web-ext run --source-dir extension
```

Serve the frontend test page
----------------------------

Serve the `frontend/` directory from the repo root:

```powershell
python -m http.server 8000 --directory frontend
```

Then open `http://localhost:8000/test-harness.html` in the same browser profile where the extension is loaded.

How the current capture flow works
----------------------------------

1. The parent page posts `GET_SCORE` or `REQUEST_VISIBLE_TAB_FROM_PARENT` into the game iframe.
2. The iframe content script either reads the score directly or forwards a visible-tab capture request to the top frame.
3. The top-frame content script asks `background.js` to run `chrome.tabs.captureVisibleTab`.
4. The extension returns a `CAPTURE_RESPONSE` message with the full tab image as a data URL plus the rectangle and DPR needed to crop the iframe region.

Permissions and compatibility notes
-----------------------------------

- The current manifest is Manifest V2.
- Requested permissions (reduced): host access only to `https://challenges.caaz.dev/*` and `https://test.challenges.caaz.dev/*`.
- For local development the manifest includes `http://localhost:5173/*` (remove this before publishing).
- No `storage`, `activeTab`, `tabs`, or `<all_urls>` permissions are requested in the reduced manifest.
- The content script runs at `document_idle` in the matched frames; `all_frames: true` is retained to support iframe injection.
- `background.js` is a background script (MV2). For Chrome Web Store publishing, migrate to Manifest V3 and convert the background script to a service worker, moving host patterns to `host_permissions`.
- If your target browser refuses to load this manifest, test with Firefox using the temporary add-on flow above or update the manifest for that browser.

Where to look for logs
----------------------

- Content script logs appear in the page DevTools for the top page or iframe where the script is running.
- Background script logs appear in the extension inspector opened from `about:debugging#/runtime/this-firefox`.

Troubleshooting
---------------

- If the content script does not run, reload the page after loading the extension and confirm `manifest.json` still matches `<all_urls>`.
- If `GET_SCORE` returns `null`, confirm the page host matches a built-in adapter and the expected score element exists in the DOM.
- If capture fails, make sure the iframe is visible in the current tab. `captureVisibleTab` only captures what the browser can currently render.
- If capture returns `iframe element not found`, the top-frame content script could not map the message source back to a DOM `iframe` element.
- `UPLOAD_IMAGE` is currently a stub message that only returns `{ ok: true }`.

Extension page API
------------------

All extension/page communication uses `window.postMessage`.

- `HAS_ADAPTER`
  Request: `{ type: 'HAS_ADAPTER', url, nonce }`
  Response: `{ type: 'HAS_ADAPTER_RESPONSE', nonce, exists, adapterName }`

- `GET_ADAPTERS`
  Request: `{ type: 'GET_ADAPTERS', nonce }`
  Response: `{ type: 'ADAPTERS_RESPONSE', nonce, adapters }`

- `GET_SCORE`
  Request: `{ type: 'GET_SCORE', nonce }`
  Response: `{ type: 'SCORE_RESPONSE', nonce, score }`

- `REQUEST_VISIBLE_TAB_FROM_PARENT`
  Request: `{ type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce }`
  Response: `{ type: 'CAPTURE_RESPONSE', nonce, dataUrl, rect, dpr }` or `{ type: 'CAPTURE_RESPONSE', nonce, error }`

Files to edit when iterating
----------------------------

- `extension/content-script.js` - built-in adapters, score reads, and message coordination between iframe and top frame
- `extension/background.js` - visible-tab capture bridge used by the content script
- `extension/manifest.json` - permissions and content script injection settings
- `frontend/test-harness.html` - manual harness for score and capture flows
