// Inject a page-context script at document_start to force preserveDrawingBuffer on WebGL contexts.
(function () {
  'use strict'
  const pageScript = '(' + function () {
    try {
      const origGet = HTMLCanvasElement.prototype.getContext;
      HTMLCanvasElement.prototype.getContext = function (type, attrs) {
        try {
          const t = String(type).toLowerCase();
          if (t === 'webgl' || t === 'experimental-webgl' || t === 'webgl2') {
            try {
              if (attrs == null) {
                attrs = { preserveDrawingBuffer: true };
              } else if (typeof attrs === 'object') {
                attrs = Object.assign({}, attrs, { preserveDrawingBuffer: true });
              }
            } catch (e) {}
          }
        } catch (e) {}
        return origGet.call(this, type, attrs);
      };

      if (typeof OffscreenCanvas !== 'undefined' && OffscreenCanvas.prototype && OffscreenCanvas.prototype.getContext) {
        const origOff = OffscreenCanvas.prototype.getContext;
        OffscreenCanvas.prototype.getContext = function (type, attrs) {
          try {
            const t = String(type).toLowerCase();
            if (t === 'webgl' || t === 'experimental-webgl' || t === 'webgl2') {
              try {
                if (attrs == null) attrs = { preserveDrawingBuffer: true };
                else if (typeof attrs === 'object') attrs = Object.assign({}, attrs, { preserveDrawingBuffer: true });
              } catch (e) {}
            }
          } catch (e) {}
          return origOff.call(this, type, attrs);
        };
      }
    } catch (e) {}
  } + ')();'

  const s = document.createElement('script');
  s.textContent = pageScript;
  (document.documentElement || document.head || document.body || document).appendChild(s);
  s.parentNode && s.parentNode.removeChild(s);
})();
