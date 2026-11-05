/**
 * True only for absolute http(s) URLs. Use before putting a URL from the API
 * into an href or iframe src, so `javascript:`/`data:` URLs never render.
 */
export function isSafeHttpUrl(value) {
  if (typeof value !== 'string') return false
  const s = value.trim()
  if (!s) return false
  let url
  try {
    url = new URL(s)
  } catch (e) {
    return false
  }
  return (url.protocol === 'http:' || url.protocol === 'https:') && Boolean(url.hostname)
}

/** The URL itself when safe, otherwise null. */
export function safeHttpUrl(value) {
  return isSafeHttpUrl(value) ? value.trim() : null
}

export default isSafeHttpUrl
