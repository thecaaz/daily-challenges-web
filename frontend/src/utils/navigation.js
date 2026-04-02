export default function goBackOrRoute(navigate, fallbackPath, { locationSearch = '', scoringDay } = {}) {
  if (typeof window !== 'undefined' && window.history && window.history.length > 1) {
    navigate(-1)
    return
  }

  const qs = locationSearch || (scoringDay ? `?scoringDay=${encodeURIComponent(scoringDay)}` : '')
  navigate(`${fallbackPath}${qs}`)
}
