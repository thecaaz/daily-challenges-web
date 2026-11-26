// Turn an axios error into a short, human-readable message.
//
// Order of preference:
//   1. `{ message }` (the backend's standard error body)
//   2. ASP.NET ProblemDetails (`errors` first entry, then `detail`, then `title`)
//   3. a short plain-text body (never HTML)
//   4. a status-based message (413, 429, 403, 404, 5xx)
//   5. the caller's fallback (also used for network errors)
//
// It never stringifies an object and never returns HTML.

const DEFAULT_FALLBACK = 'Something went wrong'
/** Anything that looks like markup (e.g. an nginx error page) is unusable. */
const MARKUP = /<[a-z!/][^>]*>/i
const MAX_TEXT_LENGTH = 300

const STATUS_MESSAGES = {
  403: "You don't have permission to do that",
  404: 'Not found',
  413: 'Screenshot too large',
  429: 'Too many attempts, please wait a minute',
}

function cleanString(value) {
  if (typeof value !== 'string') return null
  const text = value.trim()
  if (!text || text.length > MAX_TEXT_LENGTH) return null
  if (MARKUP.test(text)) return null
  return text
}

/** `errors` is ProblemDetails' `{ field: [messages] }` or an array of strings / `{ message }`. */
function firstProblemError(errors) {
  if (!errors || typeof errors !== 'object') return null
  return Object.values(errors).flat().map(problemText).find(Boolean) ?? null
}

function problemText(entry) {
  return cleanString(entry) || cleanString(entry?.message)
}

function messageFromData(data) {
  if (data == null) return null
  if (typeof data === 'string') return cleanString(data)
  if (typeof data !== 'object' || Array.isArray(data)) return null
  return (
    cleanString(data.message) ||
    cleanString(data.Message) ||
    firstProblemError(data.errors) ||
    cleanString(data.detail) ||
    cleanString(data.title) ||
    null
  )
}

function messageFromStatus(status) {
  if (!status) return null
  if (STATUS_MESSAGES[status]) return STATUS_MESSAGES[status]
  if (status >= 500) return 'Server error, please try again later'
  return null
}

/**
 * Without a response at all (network error, CORS failure, timeout, or a
 * non-axios error) the fallback is the only honest message.
 */
export function getApiErrorMessage(err, fallback = DEFAULT_FALLBACK) {
  const response = err?.response
  if (!response) return fallback
  return messageFromData(response.data) || messageFromStatus(response.status) || fallback
}

export default getApiErrorMessage
