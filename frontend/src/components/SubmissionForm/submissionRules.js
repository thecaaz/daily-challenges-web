const ALLOWED_SCREENSHOT_TYPES = ['image/png', 'image/jpeg', 'image/webp']
const MAX_SCREENSHOT_BYTES = 2 * 1024 * 1024

export const NON_NUMERIC_SCORE_WARNING = 'This score is not a number and may not show up correctly on leaderboards.'

/** The score as it is sent: the user's text, trimmed. */
export const trimScore = score => String(score ?? '').trim()

/**
 * Client-side checks before a submission is sent, in the order the user
 * should fix them. Returns the first problem as a message, or null.
 */
export function findSubmissionProblem({ isDuplicate, screenshot, score }) {
  if (isDuplicate) return 'You have already submitted for this game'
  if (!screenshot) return 'Screenshot is required'
  if (screenshot.type && !ALLOWED_SCREENSHOT_TYPES.includes(screenshot.type)) return 'Allowed formats: PNG, JPEG, WebP'
  if (screenshot.size && screenshot.size > MAX_SCREENSHOT_BYTES) return 'Screenshot must be 2 MB or smaller'
  if (!trimScore(score)) return 'Enter a score'
  return null
}
