// Origins of the DailyChallenges app that may talk to ScoreBridge.
//
// Loaded as a background script (before background.js) and as a content
// script (before content-script.js); both read globalThis.ScoreBridgeOrigins.
//
// The block between the dev-only markers is removed by extension/build.mjs
// for release builds, so a released XPI never trusts localhost.
;(function (root) {
  'use strict'

  const APP_ORIGINS = [
    'https://challenges.caaz.dev',
    'https://test.challenges.caaz.dev',
    // @dev-only-start
    'http://localhost:*',
    // @dev-only-end
  ]

  function originOf(url) {
    if (typeof url !== 'string' || url === '') return null
    try {
      return new URL(url).origin
    } catch (e) {
      return null
    }
  }

  // Entries are exact origins, or "scheme://host:*" to allow any port.
  function isAppOrigin(origin) {
    if (typeof origin !== 'string' || origin === '' || origin === 'null') return false
    return APP_ORIGINS.some(entry => {
      if (entry.endsWith(':*')) {
        const base = entry.slice(0, -2)
        return origin === base || (origin.startsWith(base + ':') && /^\d+$/.test(origin.slice(base.length + 1)))
      }
      return origin === entry
    })
  }

  root.ScoreBridgeOrigins = Object.freeze({ APP_ORIGINS: Object.freeze(APP_ORIGINS.slice()), originOf, isAppOrigin })
})(globalThis)
