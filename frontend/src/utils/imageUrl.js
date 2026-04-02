import { getApiRoot } from '../api'

export default function imageUrl(relativePath, { fallback = '' } = {}) {
  if (!relativePath) return fallback
  const root = String(getApiRoot() || '').replace(/\/$/, '')
  const path = String(relativePath).replace(/^\//, '')
  return `${root}/${path}`
}
