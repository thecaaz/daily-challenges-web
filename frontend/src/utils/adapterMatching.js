const MatchType = Object.freeze({
  INCLUDES: 'includes',
  EQUALS: 'equals',
  REGEX: 'regex',
})

/** The hostname of a URL; anything that doesn't parse as a URL is taken as a host. */
export function hostnameFromUrl(urlOrHost) {
  if (!urlOrHost) return ''
  try {
    return new URL(urlOrHost).hostname || ''
  } catch {
    return String(urlOrHost)
  }
}

/** The first adapter whose `matchDescriptor` matches `host`, or null. */
export function findAdapterForHost(adapters, host) {
  return adapters.find(adapter => matchesDescriptor(host, adapter.matchDescriptor)) || null
}

function matchesDescriptor(host, descriptor) {
  if (!descriptor || !host) return false
  const value = String(descriptor.value || '').toLowerCase()
  const lowerHost = host.toLowerCase()
  switch (descriptor.type) {
    case MatchType.INCLUDES:
      return lowerHost.includes(value)
    case MatchType.EQUALS:
      return lowerHost === value
    case MatchType.REGEX:
      return matchesPattern(descriptor.value, host)
    default:
      return false
  }
}

function matchesPattern(pattern, host) {
  try {
    return new RegExp(pattern).test(host)
  } catch {
    return false
  }
}
