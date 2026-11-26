/** Window event fired whenever a game's favourite flag changes anywhere in the app. */
export const FAVORITE_CHANGED_EVENT = 'favorite-changed'

/**
 * Tells every listener that a favourite changed.
 *
 * @param {{ gameId: number|string, isFavorite: boolean, optimistic?: true, confirmed?: true }} detail
 *   `optimistic`: shown before the server answered; `confirmed`: the server
 *   accepted it; neither: an optimistic change was rolled back.
 */
export function announceFavoriteChange(detail) {
  window.dispatchEvent(new CustomEvent(FAVORITE_CHANGED_EVENT, { detail }))
}

/**
 * Calls `handler(detail)` on every favourite change (`detail` is `{}` when the
 * event carried none).
 *
 * @returns {() => void} stops listening.
 */
export function onFavoriteChange(handler) {
  const listener = event => handler(event?.detail || {})
  window.addEventListener(FAVORITE_CHANGED_EVENT, listener)
  return () => window.removeEventListener(FAVORITE_CHANGED_EVENT, listener)
}
