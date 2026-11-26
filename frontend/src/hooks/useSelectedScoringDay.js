import { useSearchParams } from 'react-router-dom'

/** URL value for "all days" (an absent param means "the current scoring day"). */
export const ALL_DAYS = 'all'

/**
 * The scoring day shown on a game's submissions page. The URL is the source of
 * truth, so Back/Forward just work:
 *
 * - `?scoringDay=YYYY-MM-DD` -> that day
 * - `?scoringDay=all`        -> all days (`''`)
 * - absent                   -> the game's current scoring day
 *
 * @param {string} currentScoringDay - the game's current scoring day, or ''.
 * @returns {{ selectedDate: string, selectDate: (day: string) => void }}
 *   `selectDate('')` selects all days.
 */
export default function useSelectedScoringDay(currentScoringDay) {
  const [searchParams, setSearchParams] = useSearchParams()
  const scoringDayParam = searchParams.get('scoringDay')
  const selectedDate = scoringDayParam === ALL_DAYS ? '' : (scoringDayParam || currentScoringDay)

  const selectDate = (day) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      next.set('scoringDay', day || ALL_DAYS)
      return next
    })
  }

  return { selectedDate, selectDate }
}
