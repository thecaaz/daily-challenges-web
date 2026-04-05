% ScoreBridge — Iframe Screenshot Capture (PoC)

Goal
----

Deliver a minimal, working PoC that captures a target element inside an embedded iframe and returns a PNG data URL to the embedding parent page. Focus on a single, known target origin and a simple, repeatable flow.

Scope & assumptions (PoC)
------------------------

- Single target origin (you control or can inspect it) so the extension can inject a content script.
- Test pages do not contain cross‑origin images that taint canvases (or accept that capture may fail in those cases).
- No full‑tab fallback, no remote adapter updates — keep the flow minimal and testable.

PoC approach (straightforward)
------------------------------

1. Parent sends a capture request to the iframe using `postMessage({ type: 'REQUEST_IFRAME_CAPTURE', nonce, selector })` with `targetOrigin` set to the iframe origin.
2. Content script injected into the iframe listens for `REQUEST_IFRAME_CAPTURE`, renders the requested element using bundled `html2canvas`, and replies with `postMessage({ type: 'IFRAME_CAPTURE_RESPONSE', nonce, dataUrl })` to the parent.
3. Parent verifies `nonce` and `event.origin`, shows a preview, and asks the user to confirm upload (optional).

Minimal content-script sketch (PoC)
```js
window.addEventListener('message', async ev => {
	if (ev.data?.type !== 'REQUEST_IFRAME_CAPTURE') return;
	// For PoC: optionally validate ev.origin === expectedParentOrigin
	const nonce = ev.data.nonce;
	const selector = ev.data.selector || '.score-value';
	try {
		const el = document.querySelector(selector) || document.body;
		// html2canvas should be bundled into the extension for PoC
		const canvas = await window.html2canvas(el, { useCORS: true, scale: window.devicePixelRatio || 1 });
		const dataUrl = canvas.toDataURL('image/png');
		ev.source.postMessage({ type: 'IFRAME_CAPTURE_RESPONSE', nonce, dataUrl }, ev.origin);
	} catch (err) {
		ev.source.postMessage({ type: 'IFRAME_CAPTURE_RESPONSE', nonce, error: String(err) }, ev.origin);
	}
});
```

Minimal security & UX (PoC)
---------------------------

- Require `nonce` matching and prefer explicit `event.origin` checks in the parent.
- Show a preview and require explicit user confirmation before uploading images off‑device.
- Bundle `html2canvas` in `extension/vendor/` for repeatable tests (or allow CDN only during early dev).

Concrete PoC tasks
------------------

1. Add a host permission (or use `activeTab` in development) for the iframe origin so the content script can run.
2. Bundle `html2canvas.min.js` under `extension/vendor/` and load it from the content script using `browser.runtime.getURL()`.
3. Implement the content-script handler (above) and test reply behavior using `frontend/test-harness.html`.
4. Implement parent UI: send request, validate response, preview image, confirm upload.
5. Run manual tests with the sample game; iterate on selectors and timing.

Success criteria (PoC)
---------------------

- Parent receives an `IFRAME_CAPTURE_RESPONSE` with matching `nonce` and a valid PNG data URL.
- Preview displays the captured image and upload happens only after explicit confirmation.

Time estimate
-------------

1–3 hours for a single target site (shorter if the target DOM has an obvious element and no cross-origin images).

Notes / immediate limitations (PoC)
----------------------------------

- `html2canvas` can fail or taint the canvas if the page has cross‑origin images without CORS headers; for PoC, choose a test page without such images or accept a known failure case and document it.
- If a site blocks framing, run the content script in a browser tab instead of an iframe for testing; the capture handler remains the same.

