import { makeNonce, isFromContentScript, postToContentScript } from './scoreBridge'

/**
 * Posts `message` (with a fresh nonce) to the extension's content script and
 * resolves to its reply of `expectedResponseType` carrying that nonce, or to
 * null when it doesn't answer within `timeoutMs` or can't be asked at all.
 */
export function requestFromExtension(message, expectedResponseType, timeoutMs) {
  return new Promise(resolve => {
    if (!canPostMessages()) return resolve(null)

    const nonce = makeNonce()
    let timer = null
    const settle = reply => {
      window.removeEventListener('message', handleReply)
      clearTimeout(timer)
      resolve(reply)
    }
    function handleReply(event) {
      if (!isFromContentScript(event)) return
      if (event.data?.type !== expectedResponseType || event.data.nonce !== nonce) return
      settle(event.data)
    }

    window.addEventListener('message', handleReply)
    try {
      postToContentScript({ ...message, nonce })
    } catch {
      return settle(null)
    }
    timer = setTimeout(() => settle(null), timeoutMs)
  })
}

function canPostMessages() {
  return typeof window !== 'undefined' && typeof window.postMessage === 'function'
}
