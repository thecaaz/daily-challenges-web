# ScoreBridge - README

Quick summary
-------------

This directory contains a Manifest V2 Firefox extension that lets the DailyChallenges app talk to embedded game pages with `window.postMessage`. It supports four actions:

- detect whether a built-in adapter exists for a URL
- list the built-in adapters
- read a score from a supported game page
- capture a screenshot of the game iframe, cropped by Firefox, for the app that asked

Built-in adapters:

- `TimeGuessr`: reads `.summary-score` (whole numbers only), then clicks `#breakdownButton`; if the score was missing it reads once more after a short delay
- `MapTap`: reads `#ui_score`
- `Cutle`: asks an injected page script for the current `preciseRatio` on every `GET_SCORE` (validated to be a number in 0..1), with DOM fallbacks
- `BlindCut`: reads the daily best from localStorage (`blind-cut-daily-best:<UTC date>`), with a final-screen DOM fallback
- `MotionPath`: reads the final-screen score aria-label, with a per-game localStorage fallback (`?game=` selects path, dots, rhythm, or cut)

Contents
--------

- `manifest.json` - permissions and content-script injection
- `app-origins.js` - the app-origin allowlist (`globalThis.ScoreBridgeOrigins`), loaded in the background page and as the first content script
- `scoreparse.js` - score parsing (`globalThis.ScoreParse`), implementing the shared spec in `tests/fixtures/score-parsing.json`; tested from `frontend/src/test/scoreparse.test.js`
- `content-script.js` - adapters and the page message protocol
- `background.js` - the capture bridge (`tabs.captureTab`)
- `build.mjs` - release build and `updates.json` helper (see below)
- `frontend/test-harness.html` - manual harness that embeds a game page and exercises the message flow

Install and run (Firefox, development)
--------------------------------------

The source directory is the **dev build**: it also trusts `http://localhost:*` as an app origin.

Option A - temporary add-on:

1. Open `about:debugging#/runtime/this-firefox`.
2. Click **Load Temporary Add-on** and select `extension/manifest.json`.
3. Reload any already-open app or game pages so the content scripts are injected.

Option B - `web-ext`:

```sh
npx web-ext run --source-dir extension
```

Serve the test harness from localhost (the dev build only trusts localhost, any port):

```sh
python -m http.server 8000 --directory frontend
# open http://localhost:8000/test-harness.html
```

How the capture flow works
--------------------------

1. The user clicks "Submit score" in the app. The app posts `GET_SCORE {nonce}` into the game iframe (targetOrigin = the iframe's origin). The game frame's content script answers `SCORE_RESPONSE {nonce, score}` (score is a number or `null`), but only to an allowlisted app origin.
2. The app makes the iframe fill the screen, then arms the extension from its own window: `window.postMessage({ type: 'SCOREBRIDGE_ARM', nonce }, location.origin)`.
3. The app posts `REQUEST_VISIBLE_TAB_FROM_PARENT {nonce}` into the iframe. The game frame forwards `SCOREBRIDGE_VISIBLE_TAB_CAPTURE {nonce, rect}` to its parent.
4. The app-frame content script accepts it only if it runs in the **top frame of an app origin**, the message comes from an `<iframe>` of that page, and the nonce matches the armed one (single use, expires after 5 s). It computes the iframe's rectangle in page coordinates.
5. `background.js` refuses unless the request comes from the top frame (`frameId 0`) of a tab that is **active** and whose URL is an **app origin**. It then calls `browser.tabs.captureTab(tab.id, { format: 'png', rect })`, so Firefox renders only the iframe's area of exactly that tab. Without a usable rect it captures the tab's visible viewport instead.
6. The content script posts `CAPTURE_RESPONSE {nonce, dataUrl, cropped, rect, dpr}` or `{nonce, error}` to its own window only (`targetOrigin = location.origin`). Nothing is posted to the game iframe or to `'*'`.

The app (`frontend/src/hooks/useScoreCapture.js`) accepts `SCORE_RESPONSE` only from the iframe's window at the iframe's origin and `CAPTURE_RESPONSE` only from its own window, each with the nonce it sent; both nonces are single use.

Permissions
-----------

The manifest requests only `<all_urls>`. `tabs`, `alarms` and `notifications` were dropped: nothing uses them, and `sender.tab.active` / `sender.tab.url` are available without `tabs` because the extension has host permission for the app.

Why `<all_urls>` stays:

- `tabs.captureTab` is gated on `<all_urls>` in Firefox's API schema (`"permissions": ["<all_urls>"]` in `browser/components/extensions/schemas/tabs.json`); without it the function is not even exposed. MDN: "You must have the `<all_urls>` permission to use this method." Host permissions for only the app and game origins are not enough.
- The fallback, `tabs.captureVisibleTab`, needs `<all_urls>` or `activeTab`. `activeTab` is granted only by a user gesture on the extension's own UI (toolbar button, context menu, shortcut), never by a page's `postMessage`, so it cannot be used for a capture the app requests.

The content scripts are still injected only into the app and game hosts listed under `content_scripts.matches`, and the capture is restricted in code (top frame, active tab, app origin, armed nonce) as described above.

`tabs.captureTab` needs Firefox 59+; the `rect` option needs Firefox 82+.

Release build
-------------

```sh
node extension/build.mjs                # writes extension/dist (gitignored)
node extension/build.mjs --out <dir>    # or another directory
```

The release build:

- removes `http://localhost/*` (and any other localhost pattern) from `permissions` and `content_scripts.matches`
- removes the `// @dev-only-start` ... `// @dev-only-end` blocks from the JS files, which drops `http://localhost:*` from the app-origin allowlist in `app-origins.js`
- fails if a `//localhost` reference survives

Sign the build directory (for example `npx web-ext sign --source-dir extension/dist --channel unlisted`), not `extension/` itself.

Releasing a new version (signing and deploying are manual steps):

1. Bump `version` in `manifest.json` whenever the extension source changes. Leave `updates.json` and `frontend/public/extension.xpi` alone in that commit: the manifest may be ahead of the shipped XPI until the release is deployed.
2. Build the release copy: `node extension/build.mjs` (writes `extension/dist`).
3. Sign `extension/dist` (not `extension/`), e.g. `npx web-ext sign --source-dir extension/dist --channel unlisted`.
4. Replace `frontend/public/extension.xpi` with the signed XPI.
5. Deploy the app so `https://challenges.caaz.dev/extension.xpi` serves the new XPI.
6. **Only after the new XPI is live**, run `node extension/build.mjs --write-updates` and commit `updates.json`. It copies the manifest version into `updates.json`. Firefox installs whatever version `updates.json` advertises from `update_link`, so bumping it before the XPI is live makes clients fetch an old XPI under a new version (the update fails or loops).

The app's extension page (`frontend/src/pages/Extension.jsx`) shows the version from `updates.json` (the released one), not from `manifest.json`; the frontend Docker build copies `extension/` for that import.

Firefox automatic updates
-------------------------

`browser_specific_settings.gecko.update_url` points at `extension/updates.json` on the `main` branch (raw GitHub). Its single entry's `update_link` is `https://challenges.caaz.dev/extension.xpi`. Firefox checks for updates on its own schedule (default daily). See https://extensionworkshop.com/documentation/manage/updating-your-extension/

Where to look for logs
----------------------

- Content script logs appear in the page DevTools for the top page or iframe where the script is running.
- Background script logs appear in the extension inspector opened from `about:debugging#/runtime/this-firefox`.

Troubleshooting
---------------

- No `ADAPTERS_RESPONSE`: the page origin is not an app origin (release builds don't trust localhost), or the page was open before the extension loaded; reload it.
- `GET_SCORE` returns `null`: the page host doesn't match an adapter, the score element isn't rendered yet, or the request didn't come from an app origin (then there is no response at all).
- No `CAPTURE_RESPONSE`: the capture wasn't armed (or the arm expired after 5 s), or the request didn't come from an iframe of the app page. The content script logs `ignoring capture request without an armed nonce`.
- `CAPTURE_RESPONSE` with `error`: `tab is not active`, `origin not allowed`, `capture must be requested by the top frame`, or a Firefox capture error.

Extension page API
------------------

All extension/page communication uses `window.postMessage`. Requests the extension answers must come from an app origin.

- `HAS_ADAPTER` (app window)
  Request: `{ type: 'HAS_ADAPTER', url, nonce }`
  Response: `{ type: 'HAS_ADAPTER_RESPONSE', nonce, exists, adapterName }`

- `GET_ADAPTERS` (app window)
  Request: `{ type: 'GET_ADAPTERS', nonce }`
  Response: `{ type: 'ADAPTERS_RESPONSE', nonce, adapters }`

- `GET_SCORE` (into the game iframe)
  Request: `{ type: 'GET_SCORE', nonce }`
  Response: `{ type: 'SCORE_RESPONSE', nonce, score }` - `score` is a finite number or `null`

- `SCOREBRIDGE_ARM` (app window, to itself)
  Request: `{ type: 'SCOREBRIDGE_ARM', nonce }` - no response; arms one capture for 5 s

- `REQUEST_VISIBLE_TAB_FROM_PARENT` (into the game iframe, after arming with the same nonce)
  Request: `{ type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce }`
  Response (app window): `{ type: 'CAPTURE_RESPONSE', nonce, dataUrl, cropped, rect, dpr }` or `{ type: 'CAPTURE_RESPONSE', nonce, error }`. When `cropped` is true the image is already the iframe area.
