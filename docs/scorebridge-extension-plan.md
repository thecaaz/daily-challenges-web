% ScoreBridge — Browser Extension Adapter Plan

Overview
--------

This document captures a practical plan and implementation notes for a browser extension that bridges scores (or other page data) from many third-party game sites into your site.

Goals
-----

- Provide a lightweight, extensible adapter framework that runs as a browser extension.
- Let content scripts read page DOM inside third‑party frames/tabs and respond to your site via `postMessage` or report to your backend.
- Minimize upfront permissions by using optional host permissions where possible.

Architecture
------------

- Extension (Manifest V3)
  - `content_scripts` (run in all frames: `all_frames: true`) — injects per-site adapters.
  - `background` service worker — receives reports and forwards to your server.
  - `host_permissions` — requested per-site (prefer runtime request).
- Parent site — embeds iframe (if the site allows framing) and uses `postMessage` to ask for score.
- Adapter registry — small per-site modules: `match(host)`, `readScore(document)`, `observe(document, send)`.

Message protocol (recommended)
-----------------------------

- Parent → iframe: `{ source: 'ScoreBridgeParent', type: 'GET_SCORE' }` (use explicit `targetOrigin`).
- Content script → parent: `{ source: 'ScoreBridge', type: 'SCORE_RESPONSE', score }`.
- Content script → background (optional): `{ type: 'REPORT_SCORE', score, pageUrl }` via `chrome.runtime.sendMessage`.

Minimal manifest snippet (MV3)
-----------------------------

```json
{
  "manifest_version": 3,
  "name": "ScoreBridge",
  "version": "1.0",
  "permissions": ["scripting", "storage"],
  "host_permissions": [],
  "background": { "service_worker": "background.js" },
  "content_scripts": [
    {
      "matches": ["<all_urls>"],
      "js": ["content-script.js"],
      "run_at": "document_idle",
      "all_frames": true
    }
  ]
}
```

Adapter registry pattern (example)
---------------------------------

```js
const adapters = [
  {
    match: host => host.includes('timeguessr'),
    readScore: doc => {
      const el = doc.querySelector('.score-value');
      return el ? el.textContent.trim() : null;
    },
    observe: (doc, send) => {
      const obs = new MutationObserver(send);
      obs.observe(doc.body, { subtree: true, childList: true });
      return () => obs.disconnect();
    }
  }
  // more adapters...
];

function findAdapter(host) { return adapters.find(a => a.match(host)); }
```

Content script responsibilities
-------------------------------

- Select the correct adapter using `location.hostname` or `location.href`.
- Implement `postMessage` listener to respond to `{ type: 'GET_SCORE' }` from the parent iframe.
- Optionally observe DOM changes and proactively notify `window.top` or the extension background.
- Validate incoming `event.origin` and reply using explicit `targetOrigin`.

Background service responsibilities
----------------------------------

- Accept messages from content scripts (`REPORT_SCORE`) and forward to your backend API.
- Optionally hold transient state for a user session or retry failed uploads.

Parent page integration
------------------------

- Embed the third-party iframe (only possible if framing isn't blocked).
- On user submit, `postMessage({ source: 'ScoreBridgeParent', type: 'GET_SCORE' }, targetOrigin)` to the iframe.
- Listen for `SCORE_RESPONSE` from the iframe origin and submit to your server.

Permissions & UX
-----------------

- Request host permissions at runtime per-site (`chrome.permissions.request({ origins: [origin] })`) to avoid scary upfront prompts.
- Explain in your UI why you need permission and which sites are supported.

Framing limitations & alternatives
---------------------------------

- If the target blocks framing (`X-Frame-Options` or CSP `frame-ancestors`), your site cannot embed it. Alternatives:
  - Run the extension when the user opens the game tab (content script still works in tabs).
  - Use an extension background relay or popup to collect data.
  - Use a server-side headless browser (Puppeteer) or a reverse proxy (note ToS/legal implications).

Security & privacy
------------------

- Always validate `event.origin` in `message` handlers.
- Avoid using `'*'` as `targetOrigin` for sensitive messages.
- Minimize data sent to your server and document retention in your privacy policy.

Maintenance & robustness
------------------------

- DOM selectors will break; keep adapters small and easy to update.
- Consider a remote-updatable adapter registry or store adapters centrally (careful with code execution risks).
- Use `MutationObserver` + debounced reads rather than naive polling where possible.

Distribution notes
------------------

- Browsers and stores scrutinize extensions that request many host permissions. Use optional runtime permissions and clear disclosure.
- For internal/private use, distribute as an unpacked extension; for public distribution follow Chrome/Firefox store policies.

Next steps
----------

1. Scaffold the MV3 extension (`manifest.json`, `content-script.js`, `background.js`, `adapters/`).
2. Implement one adapter (TimeGuessr) and test locally with a willing user installing the extension.
3. Add an onboarding UI that requests per-site permission at runtime.

---

If you want, I can scaffold the extension files in this repo now (manifest, example adapter, content script, and background). Tell me whether to create the files here, and which adapter (TimeGuessr or another) to start with.
