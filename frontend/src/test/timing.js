/**
 * Waits a few real milliseconds so pending promises, effects and timers can
 * run. Use it before asserting that something did *not* happen.
 */
export function letPendingWorkSettle(ms = 20) {
  return new Promise(resolve => setTimeout(resolve, ms))
}
