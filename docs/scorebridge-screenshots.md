% ScoreBridge — Screenshot Capture Plan

Overview
--------

This document describes two practical approaches for capturing screenshots of third‑party game pages from a browser extension: (A) element‑level rendering inside the page (e.g. html2canvas) and (B) full‑tab screenshot + crop (via `chrome.tabs.captureVisibleTab`).

When to use which
------------------
- Element render (`html2canvas`): simpler, no `tabs` permission, runs entirely in the content script. Works best when the element is accessible and images are CORS-friendly.
- Full‑tab capture + crop: highest fidelity (exact pixels), avoids canvas tainting from cross‑origin images, but requires `tabs` (or `activeTab`) permission and careful coordinate handling.

1) Element render (content script + html2canvas)
-------------------------------------------------

Flow
- Content script (injected into target frame) finds the score element.
- Use `html2canvas(element, { useCORS: true, scale: window.devicePixelRatio || 1 })` to render a canvas.
- Convert to PNG data URL and send to background for upload or directly POST from content script.

Example (content-script.js)
```js
async function captureElement(selector) {
  const el = document.querySelector(selector);
  if (!el) throw new Error('element not found');
  // html2canvas must be bundled/injected into the page by the extension
  const canvas = await html2canvas(el, { useCORS: true, scale: window.devicePixelRatio || 1 });
  const dataUrl = canvas.toDataURL('image/png');
  chrome.runtime.sendMessage({ type: 'UPLOAD_IMAGE', dataUrl, pageUrl: location.href });
}

// example usage
captureElement('.score-value').catch(console.error);
```

Pros / Cons
- Pros: simple, no `tabs` permission, runs fully inside the frame.
- Cons: `html2canvas` may not reproduce everything perfectly; cross‑origin images without CORS will taint the canvas and prevent export.

Tips
- Bundle `html2canvas` with the extension or inject it into pages where needed.
- Use `MutationObserver` to wait for the score element to appear instead of naive polling.

2) Full‑tab screenshot + crop (background + content script)
---------------------------------------------------------

Flow
- Content script computes bounding rect of the element: `el.getBoundingClientRect()`.
- Content script sends a message to the background: `{ type: 'CAPTURE_TAB', rect, dpr: window.devicePixelRatio }`.
- Background calls `chrome.tabs.captureVisibleTab(...)` to get a full-viewport PNG data URL and returns it.
- Content script loads the returned image, draws it to a canvas, crops to the rect (accounting for DPR), and obtains a cropped PNG data URL for upload.

Background (service worker) example
```js
chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg?.type === 'CAPTURE_TAB') {
    // sender.tab is required; captureVisibleTab needs the windowId in some contexts
    chrome.tabs.captureVisibleTab(sender.tab.windowId, { format: 'png' }, dataUrl => {
      sendResponse({ dataUrl });
    });
    return true; // keep channel open for async response
  }
  if (msg?.type === 'UPLOAD_IMAGE') {
    fetch('https://your.backend/score-image', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ image: msg.dataUrl, pageUrl: msg.pageUrl })
    }).catch(console.error);
  }
});
```

Content script (crop) example
```js
const rect = document.querySelector('.score-value').getBoundingClientRect();
chrome.runtime.sendMessage({ type: 'CAPTURE_TAB', rect, dpr: window.devicePixelRatio }, resp => {
  const img = new Image();
  img.onload = () => {
    const scale = window.devicePixelRatio || 1;
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(rect.width * scale);
    canvas.height = Math.round(rect.height * scale);
    const ctx = canvas.getContext('2d');
    ctx.drawImage(img,
      Math.round(rect.left * scale), Math.round(rect.top * scale),
      Math.round(rect.width * scale), Math.round(rect.height * scale),
      0, 0, canvas.width, canvas.height
    );
    const cropped = canvas.toDataURL('image/png');
    chrome.runtime.sendMessage({ type: 'UPLOAD_IMAGE', dataUrl: cropped, pageUrl: location.href });
  };
  img.src = resp.dataUrl;
});
```

Pros / Cons
- Pros: pixel‑accurate, avoids `html2canvas` limitations and cross‑origin image tainting.
- Cons: requires `tabs` or `activeTab` permission; careful coordinate math required with DPR and nested frames.

Coordinate notes for nested frames
- If the element is inside a nested iframe, `getBoundingClientRect()` returns coordinates relative to that frame's viewport. To compute coordinates relative to the top-level capture, you must accumulate frame offsets (via `window.frameElement` traversal) or have the top-level frame compute coordinates via a postMessage chain.

Permissions & runtime UX
------------------------
- `host_permissions`: for content script injection into target sites (request per‑site at runtime to improve install UX).
- `tabs` or `activeTab`: required for `chrome.tabs.captureVisibleTab`. `activeTab` grants temporary access when user interacts with extension UI; `tabs` is broader.
- Avoid asking for broad permissions up front; request runtime permissions for each site when needed.

Security, privacy & store policy
-------------------------------
- Screenshots can contain sensitive user data. Obtain explicit consent and clearly describe what is sent to your backend.
- Minimize retention and store only what you need.
- When publishing, document host permissions and screenshot usage in the store listing and privacy policy.

Fallbacks & alternatives
------------------------
- If framing is blocked, the extension can still capture when the user opens the target page in a separate tab.
- For fully automated server-side capture, consider headless browsers (Puppeteer); be cautious about ToS/legal implications.

Next steps
----------
1. Choose primary capture strategy (`html2canvas` element render or `captureVisibleTab` crop).
2. Add example capture code into the extension scaffold under `content-scripts/` and `background.js`.
3. Implement runtime permission requests and a privacy/consent UX before uploading images.
