% ScoreBridge — PoC Plan

Objective
---------

Build a minimal proof‑of‑concept that reads a game's score and (optionally) captures a screenshot from a third‑party page using a browser extension + a small parent test harness. The PoC targets a single game (TimeGuessr) and uses element render (`html2canvas`) as the default capture method.

Scope (PoC)
------------
- Single target site: TimeGuessr (https://timeguessr.com/roundonedaily).
- Minimal permissions: `activeTab` and per‑site host permission requested at runtime when needed.
- No remote adapter updates; ship adapters bundled in the extension.
- Manual user consent/preview required before any screenshot upload.

Deliverables
------------
- `extension/` (MV3) with:
  - `manifest.json`
  - `content-script.js`
  - `background.js`
  - `adapters/timeguessr.js`
  - `vendor/html2canvas.min.js` (or bundle via a build step)
- `frontend/test-harness.html` — simple page with an iframe + Submit button to exercise `postMessage` flow.
- `docs/` additions (this PoC plan + test notes).

Concrete tasks & steps
----------------------

1) Scaffold extension folder
- Create `extension/manifest.json` (MV3). Example snippet:

```json
{
  "manifest_version": 3,
  "name": "ScoreBridge-PoC",
  "version": "0.1.0",
  "permissions": ["scripting", "storage", "activeTab"],
  "background": { "service_worker": "background.js" },
  "content_scripts": [
    {
      "matches": ["https://REPLACE_WITH_GAME_ORIGIN/*"],
      "js": ["content-script.js"],
      "run_at": "document_idle",
      "all_frames": true
    }
  ]
}
```

2) Implement a tiny adapter (adapters/timeguessr.js)
- Export (or expose) three minimal items: `match(host)`, `readScore(doc)`, `captureSelector` (CSS selector of score element).
- PoC `readScore` should be defensive (try multiple selectors, trim, parse numbers). Keep logic simple and testable.

3) Implement `content-script.js`
- On load: pick adapter by `location.hostname` (or `location.href`).
- Listen for `message` events from parent frames (verify `event.origin`) for `{ type: 'GET_SCORE', nonce }` and reply `{ type: 'SCORE_RESPONSE', nonce, score }`.
- Implement `capture` handler that uses bundled `html2canvas` to render the adapter's `captureSelector` and returns a data URL.
- For robustness: use a `MutationObserver` with debounce to detect when score appears.

Minimal content-script pseudocode
```js
const adapter = findAdapter(location.hostname);
window.addEventListener('message', async (ev) => {
  if (ev.data?.type === 'GET_SCORE') {
    if (ev.origin !== 'https://your.test.origin') return;
    const score = adapter ? adapter.readScore(document) : null;
    ev.source.postMessage({ type: 'SCORE_RESPONSE', nonce: ev.data.nonce, score }, ev.origin);
  }
  if (ev.data?.type === 'CAPTURE') {
    const el = document.querySelector(adapter.captureSelector);
    const canvas = await html2canvas(el, { useCORS: true, scale: window.devicePixelRatio || 1 });
    const dataUrl = canvas.toDataURL('image/png');
    ev.source.postMessage({ type: 'CAPTURE_RESPONSE', nonce: ev.data.nonce, dataUrl }, ev.origin);
  }
});
```

4) Implement `background.js`
- Accept `chrome.runtime.onMessage` for `UPLOAD_IMAGE` and POST to `https://your.backend/score-image` (or let content script POST directly for PoC).
- Keep logic minimal to avoid MV3 lifetime issues: prefer content‑script cropping/encoding and direct `fetch` from content script.

5) Parent test harness (frontend/test-harness.html)
- Embed the real game iframe (if allowed) or instruct dev to open the game in a tab.
- Submit flow (on click): generate a `nonce`, `iframe.contentWindow.postMessage({ type:'GET_SCORE', nonce }, targetOrigin)`, wait for `SCORE_RESPONSE` and display result. For screenshot, send `CAPTURE` message and present preview to user before upload.

6) Local dev/test steps
- Load extension unpacked in Chrome/Chromium: `chrome://extensions` → Developer mode → Load unpacked → select `extension/`.
- Open `frontend/test-harness.html` on a local webserver (or use file:// for quick tests if `postMessage` targetOrigin is adjusted).
- Walk through `Submit` flow and confirm score + preview.

7) Success criteria (PoC)
- The extension successfully reads the score from the target game and the parent receives the same value via `postMessage`.
- The extension can produce a screenshot data URL of the score element and the user can preview and confirm upload.
- No broad host permissions are requested at install; permissions are minimal and request per‑site during testing.

8) Timebox & estimate
- Estimated effort: 4–8 hours to get a working PoC (scaffold + TimeGuessr adapter + test harness + manual testing). More time if the target site uses canvas or obfuscated markup.

9) Risks & mitigations (PoC focus)
- Fragile selectors: keep adapter simple; add quick selector dev tools in extension popup to iterate.
- Framing blocked: if embedding fails, test by opening the game in its own tab and use the extension there.
- Canvas/Shadow DOM: if the score is rendered to canvas, consider reading site JS globals (adapters can attempt to read `window.__SCORE__` if present) or fall back to full‑tab capture.

10) Next steps after PoC
- If PoC succeeds: add 1–2 more adapters, create a small adapter test harness (Playwright snapshots), and design a secure adapter update/packaging model.

Checklist to begin
------------------
- [ ] Pick exact TimeGuessr origin and verify page structure.
- [ ] Create `extension/` scaffold and add `html2canvas` (bundled or local vendor file).
- [ ] Implement `adapters/timeguessr.js` with selectors and `readScore`.
- [ ] Implement `content-script.js` and `frontend/test-harness.html`.
- [ ] Load extension and manually test the flow.

Notes
-----
- Keep user privacy front and center: always show a screenshot preview and require explicit user confirmation before sending images off‑device. Document the flow in the extension's privacy text.

If you want, I can scaffold the `extension/` files and the `frontend/test-harness.html` now (TimeGuessr + `html2canvas`). Tell me to proceed and provide the exact target origin for the adapter.
