// Only allow in-app redirect targets: a path that starts with a single '/'.
// Rejects absolute URLs ('https://evil.com'), protocol-relative URLs
// ('//evil.com'), backslash tricks ('/\evil.com') and control characters.
export function getSafeRedirect(value, fallback = '/') {
  if (typeof value !== 'string' || value.length === 0) return fallback
  if (!value.startsWith('/')) return fallback
  if (value.startsWith('//') || value.startsWith('/\\')) return fallback
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001f\u007f]/.test(value)) return fallback
  return value
}

export function isSafeRedirect(value) {
  return getSafeRedirect(value, null) !== null
}

// Build a login/register URL that carries the redirect target along.
export function withRedirect(path, redirect) {
  const safe = getSafeRedirect(redirect, null)
  if (!safe || safe === '/') return path
  return `${path}?redirect=${encodeURIComponent(safe)}`
}

export default getSafeRedirect
