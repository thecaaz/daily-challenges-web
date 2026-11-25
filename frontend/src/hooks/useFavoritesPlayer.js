import { useCallback, useRef, useState } from 'react'
import usePlayableFavorites from './usePlayableFavorites'
import { useScoreCapture } from './useScoreCapture'
import { isSafeHttpUrl } from '../utils/safeUrl'

/**
 * Plays the user's pending, extension-capturable favorite games one after
 * another, capturing each score with the ScoreBridge extension.
 *
 * - The queue is computed once per user (no reload on `favorite-changed`),
 *   so it never reshuffles while playing.
 * - `advance()` leaves the current game: it abandons a capture still in
 *   flight (its late responses are ignored) and drops any captured
 *   score/screenshot so they can't be submitted for the next game.
 * - `currentUrl` is null when the game has no safe http(s) URL.
 *
 * @param {number|undefined} userId
 * @param {{ enabled: boolean }} options - false while auth is still loading.
 */
export default function useFavoritesPlayer(userId, { enabled }) {
  const favorites = usePlayableFavorites(userId, { enabled, subscribe: false })
  const queue = favorites.playable
  const [currentIndex, setCurrentIndex] = useState(0)
  const current = queue[currentIndex]
  const currentUrl = current && isSafeHttpUrl(current.url) ? current.url : null

  const iframeRef = useRef(null)
  const [isMaximized, setIsMaximized] = useState(false)
  const capture = useScoreCapture(iframeRef, { isMaximized, url: currentUrl })
  const { cancelCapture, setScore, setCapturedFile, setShowSubmission } = capture

  const discardCapturedScore = useCallback(() => {
    setShowSubmission(false)
    setCapturedFile(null)
    setScore('')
  }, [setScore, setCapturedFile, setShowSubmission])

  const advance = useCallback(() => {
    cancelCapture()
    discardCapturedScore()
    setCurrentIndex(index => index + 1)
  }, [cancelCapture, discardCapturedScore])

  return {
    favorites,
    queue,
    currentIndex,
    current,
    currentUrl,
    isFinished: currentIndex >= queue.length,
    iframeRef,
    isMaximized,
    setIsMaximized,
    capture,
    advance,
    cancelSubmission: discardCapturedScore,
  }
}
