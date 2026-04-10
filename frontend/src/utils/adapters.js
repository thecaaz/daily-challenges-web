// Adapter helper: prefer asking the browser extension for adapters via
// window.postMessage. Falls back to an empty list when the extension
// doesn't respond.

let cachedAdapters = null

function hostnameFromUrl(urlOrHost) {
  if (!urlOrHost) return ''
  try {
    const u = new URL(urlOrHost)
    return u.hostname || ''
  } catch (e) {
    return String(urlOrHost)
  }
}

function matchDescriptorMatch(host, descriptor) {
  if (!descriptor || !host) return false
  const val = String(descriptor.value || '').toLowerCase()
  const h = host.toLowerCase()
  switch (descriptor.type) {
    case 'includes':
      return h.includes(val)
    case 'equals':
      return h === val
    case 'regex':
      try { return new RegExp(descriptor.value).test(host) } catch (e) { return false }
    default:
      return false
  }
}

function makeNonce() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

function requestFromExtension(message, expectedResponseType, timeoutMs = 800) {
  return new Promise(resolve => {
    if (typeof window === 'undefined' || typeof window.postMessage !== 'function') {
      return resolve(null)
    }

    const nonce = makeNonce()
    const handler = (ev) => {
      try {
        if (!ev.data || typeof ev.data.type !== 'string') return
        if (ev.data.nonce !== nonce) return
        if (ev.data.type === expectedResponseType) {
          window.removeEventListener('message', handler)
          resolve(ev.data)
        }
      } catch (e) {
        // ignore
      }
    }

    window.addEventListener('message', handler)

    try {
      window.postMessage(Object.assign({}, message, { nonce }), '*')
    } catch (e) {
      window.removeEventListener('message', handler)
      return resolve(null)
    }

    setTimeout(() => {
      window.removeEventListener('message', handler)
      resolve(null)
    }, timeoutMs)
  })
}

export async function fetchAdapters({ timeoutMs = 800 } = {}) {
  if (cachedAdapters !== null) return cachedAdapters

  const resp = await requestFromExtension({ type: 'GET_ADAPTERS' }, 'ADAPTERS_RESPONSE', timeoutMs)
  if (resp && Array.isArray(resp.adapters)) {
    cachedAdapters = resp.adapters
    return cachedAdapters
  }

  // No extension response; cache empty list
  cachedAdapters = []
  return cachedAdapters
}

export function getCachedAdapters() {
  return cachedAdapters || []
}

export async function getAdapterForUrl(urlOrHost) {
  const host = hostnameFromUrl(urlOrHost)
  if (!host) return null

  const adapters = await fetchAdapters()
  if (!adapters || adapters.length === 0) return null

  return adapters.find(a => matchDescriptorMatch(host, a.matchDescriptor)) || null
}

export async function hasAdapterForUrl(urlOrHost) {
  // Try cached adapters first
  const a = await getAdapterForUrl(urlOrHost)
  if (a) return true

  // As a fallback, ask the extension directly which may respond faster
  const resp = await requestFromExtension({ type: 'HAS_ADAPTER', url: urlOrHost }, 'HAS_ADAPTER_RESPONSE')
  if (resp && typeof resp.exists === 'boolean') return resp.exists
  return false
}

export default { fetchAdapters, getCachedAdapters, getAdapterForUrl, hasAdapterForUrl }
