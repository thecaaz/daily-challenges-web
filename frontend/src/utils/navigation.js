// Go back if there is an in-app history entry to go back to, otherwise route to
// `fallbackPath`. React Router gives the first location of a session the key
// 'default', so any other key means we navigated here from within the app.
// (window.history.length also counts other sites' entries, so it can't be used.)
export function canGoBack(location) {
  return !!location && !!location.key && location.key !== 'default'
}

export default function goBackOrRoute(navigate, location, fallbackPath, { locationSearch = '', scoringDay } = {}) {
  if (canGoBack(location)) {
    navigate(-1)
    return
  }

  const qs = locationSearch || (scoringDay ? `?scoringDay=${encodeURIComponent(scoringDay)}` : '')
  navigate(`${fallbackPath}${qs}`)
}
