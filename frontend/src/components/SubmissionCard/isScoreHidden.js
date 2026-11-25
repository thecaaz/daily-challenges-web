/**
 * The server masks today's scores while the viewer hasn't submitted yet:
 * score, rank and screenshot are then null or missing.
 */
export function isScoreHidden(submission) {
  return submission.score == null || submission.score === ''
}
