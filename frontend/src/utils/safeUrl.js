/**
 * True only for absolute http(s) URLs. Use before putting a URL from the API
 * into an href or iframe src, so `javascript:`/`data:` URLs never render.
 */
export function isSafeHttpUrl(value) {
  if (typeof value !== 'string') return false
  const text = value.trim()
  if (!text) return false
  let url
  try {
    url = new URL(text)
  } catch {
    return false
  }
  return (url.protocol === 'http:' || url.protocol === 'https:') && Boolean(url.hostname)
}

/** The URL itself when safe, otherwise null. */
export function safeHttpUrl(value) {
  return isSafeHttpUrl(value) ? value.trim() : null
}

export default isSafeHttpUrl
