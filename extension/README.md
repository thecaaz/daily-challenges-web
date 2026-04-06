# ScoreBridge-PoC — README (Firefox)

Quick summary
-------------

This repository contains a small proof‑of‑concept browser extension that demonstrates reading a game's score and capturing a screenshot of the score element from a third‑party page (element render via html2canvas). This README focuses on running and debugging the PoC in Firefox.

Contents
--------

- `extension/` — extension source (manifest, content script, background).
- `frontend/sample-game.html` — simple demo page that exposes a score element.
- `frontend/test-harness.html` — harness that embeds the sample game and requests the score via `postMessage`.

Install & run (Firefox)
------------------------

Option A — Load as a temporary add‑on (quick, no extra tooling):

1. Open Firefox on desktop.
2. Navigate to `about:debugging#/runtime/this-firefox`.
3. Click **Load Temporary Add‑on**.
4. In the file picker select the `extension/manifest.json` file from this repo (or any file inside `extension/`).

Notes:
- The add‑on loaded this way is temporary and will be removed when Firefox restarts.
- After loading, reload any pages you want the content script to run on so the extension injects.

Option B — Use Mozilla `web-ext` for a development runner (recommended for iterative work):

```powershell
# install once (or use npx)
npm install --global web-ext

# run a temporary Firefox instance with the extension loaded
npx web-ext run --source-dir extension
```

`web-ext run` opens a disposable Firefox profile and reloads the extension automatically when files change.

Serve the frontend test pages
----------------------------

Serve the `frontend/` folder so the test harness runs reliably (recommended):

```powershell
# from repo root
python -m http.server 8000 --directory frontend
# then open http://localhost:8000/test-harness.html in Firefox
```

Notes about html2canvas and offline testing
-----------------------------------------

- The content script currently injects `html2canvas` from a CDN for quick PoC use. For offline or deterministic behavior, download `html2canvas.min.js` and place it under `extension/vendor/html2canvas.min.js`, then modify the loader in `content-script.js` to use `browser.runtime.getURL('vendor/html2canvas.min.js')` instead of the CDN.

Permissions & compatibility notes
--------------------------------

- The PoC manifest requests `activeTab`, `scripting`, and `storage`. Firefox support for some MV3 APIs may vary by release; if you encounter compatibility errors, try Firefox Developer Edition or Nightly, or run via `web-ext` which uses a compatible runtime.
- If MV3 APIs present issues in your environment, you can test with a manifest v2 background script (not part of this PoC) or run the PoC content script via `scripting.executeScript` from `web-ext` commands.

Where to look for logs & debugging
---------------------------------

- Content script logs: open DevTools on the page (the target iframe or test harness) to see console output from the content script.
- Background/service worker logs: go to `about:debugging#/runtime/this-firefox`, find the loaded extension and click **Inspect** to view the background console.

Troubleshooting
---------------

- If the content script appears not to run:
  - Ensure the extension is loaded and the page is reloaded after loading the temporary add‑on.
  - Check the `matches` rules in `extension/manifest.json`; for local testing you may need to load the sample game through `http://localhost:8000` as above.
- If capture fails or html2canvas errors:
  - Confirm the CDN is reachable or bundle `html2canvas` locally as described above.
  - Cross‑origin images can taint the canvas and prevent export; for those pages consider a full‑tab capture fallback (requires different permissions).

Privacy & safety (PoC)
----------------------

- The PoC shows screenshot previews locally and does not upload images by default. If you enable uploads, obtain explicit user consent and document retention policies.

Next steps (if PoC successful)
-----------------------------

- Add additional adapters and automated adapter tests (Playwright/Puppeteer snapshots).
- Replace CDN loading with bundled `vendor/` files for stability and offline dev.
- Add per‑site runtime permission requests and a user onboarding flow.

Extension page API
------------------

The content script exposes a small `postMessage`-based API so pages can query which adapters the extension knows about and ask whether an adapter exists for an arbitrary URL.

- `HAS_ADAPTER` (page → content script)
  - Payload: `{ type: 'HAS_ADAPTER', url: '<url>', nonce: '<nonce>' }`
  - Response: `{ type: 'HAS_ADAPTER_RESPONSE', nonce, exists: true|false, adapterName: string|null }

- `GET_ADAPTERS` (page → content script)
  - Payload: `{ type: 'GET_ADAPTERS', nonce: '<nonce>' }`
  - Response: `{ type: 'ADAPTERS_RESPONSE', nonce, adapters: [ { name, matchDescriptor } ] }`

Example (page):

```javascript
const nonce = Date.now() + Math.random();
window.addEventListener('message', ev => {
  if (!ev.data || typeof ev.data.type !== 'string') return;
  if (ev.data.type === 'HAS_ADAPTER_RESPONSE' && ev.data.nonce === nonce) {
    console.log('has adapter?', ev.data.exists, ev.data.adapterName);
  }
  if (ev.data.type === 'ADAPTERS_RESPONSE') {
    console.log('adapters', ev.data.adapters);
  }
});
window.postMessage({ type: 'HAS_ADAPTER', url: 'https://timeguessr.com', nonce }, '*');
window.postMessage({ type: 'GET_ADAPTERS', nonce }, '*');
```

Security note: exposing adapter metadata can make it easier for pages to fingerprint installed extensions. Consider restricting responses (for example only responding to same-origin pages) if this is a concern.

Files to edit when iterating
---------------------------

- `extension/content-script.js` — adapters, capture logic, message handlers.
- `extension/background.js` — backend forwarding (currently logs requests for PoC).
- `frontend/test-harness.html` — harness to exercise `postMessage` flows.
