// Origins of the DailyChallenges app that may talk to ScoreBridge.
//
// Loaded as a background script (before background.js) and as a content
// script (before content-script.js); both read globalThis.ScoreBridgeOrigins.
//
// The block between the dev-only markers is removed by extension/build.mjs
// for release builds, so a released XPI never trusts localhost.
;(function (root) {
  'use strict'

  // Entries are exact origins, or "scheme://host:*" to allow any port.
  const APP_ORIGINS = [
    'https://challenges.caaz.dev',
    'https://test.challenges.caaz.dev',
    // @dev-only-start
    'http://localhost:*',
    // @dev-only-end
  ]

  const ANY_PORT_SUFFIX = ':*'
  const OPAQUE_ORIGIN = 'null'
  const PORT_NUMBER = /^\d+$/

  /** The origin of `url`, or null when it is empty or not a URL. */
  function originOf(url) {
    if (typeof url !== 'string' || url === '') return null
    try {
      return new URL(url).origin
    } catch (notAUrl) {
      return null
    }
  }

  function isAppOrigin(origin) {
    if (typeof origin !== 'string' || origin === '' || origin === OPAQUE_ORIGIN) return false
    return APP_ORIGINS.some(entry => matchesEntry(origin, entry))
  }

  function matchesEntry(origin, entry) {
    if (!entry.endsWith(ANY_PORT_SUFFIX)) return origin === entry
    return matchesAnyPort(origin, entry.slice(0, -ANY_PORT_SUFFIX.length))
  }

  function matchesAnyPort(origin, base) {
    if (origin === base) return true
    const portPrefix = base + ':'
    return origin.startsWith(portPrefix) && PORT_NUMBER.test(origin.slice(portPrefix.length))
  }

  root.ScoreBridgeOrigins = Object.freeze({ APP_ORIGINS: Object.freeze(APP_ORIGINS.slice()), originOf, isAppOrigin })
})(globalThis)
