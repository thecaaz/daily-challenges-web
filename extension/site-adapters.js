// ScoreBridge built-in site adapters.
//
// Loaded as a content script after scoreparse.js (globalThis.ScoreParse) and
// before content-script.js, which reads globalThis.ScoreBridgeAdapters.
// An adapter's readScore(doc) returns a number, null, or a promise of either;
// its optional onInit(doc) runs once when the content script starts on a
// matching page.
;(function (root) {
  'use strict'

  const { readNumber } = root.ScoreParse

  const MATCH_INCLUDES = 'includes'
  const ISO_DATE_LENGTH = 10
  const HUNDREDTHS = 100

  const TIMEGUESSR_SCORE_SELECTORS = ['.summary-score']
  const TIMEGUESSR_BREAKDOWN_BUTTON = '#breakdownButton'
  const TIMEGUESSR_RETRY_DELAY_MS = 150
  const LEFT_SINGLE_QUOTES = /\u2018/g

  const MAPTAP_SCORE_SELECTORS = ['#ui_score']

  const CUTLE_RATIO_TIMEOUT_MS = 500
  const CUTLE_INJECTOR_ID = 'cutle-precise-ratio-injector'
  const CUTLE_PAIR_CONTAINERS = ['#result', '#statsScore']
  const RATIO_TO_HUNDREDTHS_OF_PERCENT = 10000
  const NON_NUMBER_CHARS = /[^0-9.\-]/g
  const DECIMAL_NUMBERS = /-?\d+(?:\.\d+)?/g
  const CutleMessage = Object.freeze({
    REQUEST_RATIO: 'CUTLE_REQUEST_RATIO',
    PRECISE_RATIO: 'CUTLE_PRECISE_RATIO',
  })

  const BLINDCUT_BEST_KEY_PREFIX = 'blind-cut-daily-best:'
  const BLINDCUT_FINAL_SCORE = '.final-screen .final-score strong'

  const MOTIONPATH_FINAL_SCORE = '.final-screen .final-score'
  const MOTIONPATH_GAME_PARAM = 'game'
  const MOTIONPATH_DEFAULT_GAME = 'path'
  const MOTIONPATH_DAILY_KEYS = {
    path: 'motion-path.daily.v1',
    dots: 'dot-memory.daily.v1',
    rhythm: 'rhythm.daily.v1',
    cut: 'blind-cut.daily.v1'
  }

  const ADAPTERS = [
    { name: 'TimeGuessr', matchDescriptor: includes('timeguessr'), readScore: readTimeGuessrScore },
    { name: 'MapTap', matchDescriptor: includes('maptap'), readScore: readMapTapScore },
    { name: 'Cutle', matchDescriptor: includes('pfiffel'), onInit: injectCutleRatioScript, readScore: readCutleScore },
    { name: 'BlindCut', matchDescriptor: includes('blindcut'), readScore: readBlindCutScore },
    { name: 'MotionPath', matchDescriptor: includes('motionpath'), readScore: readMotionPathScore },
  ]

  const cutleRatioWaiters = []
  window.addEventListener('message', resolveCutleRatioWaiters)

  root.ScoreBridgeAdapters = Object.freeze({ findAdapter, describeAdapters })

  /** The adapter for `host` (any case), or null. */
  function findAdapter(host) {
    const lowerHost = String(host || '').toLowerCase()
    return ADAPTERS.find(adapter => lowerHost.includes(adapter.matchDescriptor.value)) || null
  }

  /** What the app may know about each adapter: its name and match descriptor. */
  function describeAdapters() {
    return ADAPTERS.map(adapter => ({ name: adapter.name, matchDescriptor: adapter.matchDescriptor }))
  }

  function includes(value) {
    return { type: MATCH_INCLUDES, value }
  }

  /**
   * Reads before opening the breakdown, because opening it re-renders the
   * summary; when the score was missing, reads once more a little later.
   */
  async function readTimeGuessrScore(doc) {
    const score = readTimeGuessrSummary(doc)
    openTimeGuessrBreakdown(doc)
    if (score !== null) return score
    await delay(TIMEGUESSR_RETRY_DELAY_MS)
    return readTimeGuessrSummary(doc)
  }

  /** TimeGuessr scores are whole numbers in 0..50000, so any separator groups thousands. */
  function readTimeGuessrSummary(doc) {
    return readFirstScore(doc, TIMEGUESSR_SCORE_SELECTORS, element =>
      readNumber(String(element.textContent).replace(LEFT_SINGLE_QUOTES, "'"), { integerOnly: true })
    )
  }

  function openTimeGuessrBreakdown(doc) {
    const breakdownButton = doc.querySelector(TIMEGUESSR_BREAKDOWN_BUTTON)
    if (breakdownButton) breakdownButton.click()
  }

  function readMapTapScore(doc) {
    return readFirstScore(doc, MAPTAP_SCORE_SELECTORS, element => readNumber(element.textContent))
  }

  /**
   * Cutle keeps its ratio in a page-scoped `let`, which the isolated
   * content-script world cannot read. This page script answers each
   * CUTLE_REQUEST_RATIO with the current value, so a score is never read from
   * a stale cache.
   */
  function injectCutleRatioScript(doc) {
    if (doc.getElementById(CUTLE_INJECTOR_ID)) return
    const script = doc.createElement('script')
    script.id = CUTLE_INJECTOR_ID
    script.textContent = `
          (function () {
            if (window.__scoreBridgeCutle) return
            window.__scoreBridgeCutle = true
            window.addEventListener('message', function (ev) {
              if (ev.source !== window || !ev.data || ev.data.type !== '${CutleMessage.REQUEST_RATIO}') return
              var ratio = null
              try { ratio = typeof preciseRatio !== 'undefined' ? preciseRatio : null } catch (e) {}
              window.postMessage({ type: '${CutleMessage.PRECISE_RATIO}', ratio: ratio }, location.origin)
            })
          })()
        `
    ;(doc.head || doc.documentElement || doc.body).appendChild(script)
  }

  /** The smaller side of the cut in percent, from the page ratio or else from the DOM. */
  async function readCutleScore(doc) {
    const ratio = await requestCutleRatio()
    if (ratio !== null) return smallerSidePercent(ratio)
    for (const selector of CUTLE_PAIR_CONTAINERS) {
      const score = readCutlePair(doc.querySelector(selector))
      if (score !== null) return score
    }
    return null
  }

  function smallerSidePercent(ratio) {
    const smaller = Math.min(ratio, 1 - ratio)
    return Math.round(smaller * RATIO_TO_HUNDREDTHS_OF_PERCENT) / HUNDREDTHS
  }

  /** Resolves to the page's current ratio, or null when it doesn't answer in time. */
  function requestCutleRatio() {
    return new Promise(resolve => {
      let isSettled = false
      const settle = value => {
        if (isSettled) return
        isSettled = true
        resolve(value)
      }
      cutleRatioWaiters.push(settle)
      setTimeout(() => settle(null), CUTLE_RATIO_TIMEOUT_MS)
      try {
        window.postMessage({ type: CutleMessage.REQUEST_RATIO }, location.origin)
      } catch (postFailed) {
        settle(null)
      }
    })
  }

  function resolveCutleRatioWaiters(ev) {
    if (ev.source !== window || ev.origin !== location.origin) return
    const data = ev.data
    if (!data || data.type !== CutleMessage.PRECISE_RATIO) return
    const ratio = isRatio(data.ratio) ? data.ratio : null
    while (cutleRatioWaiters.length) cutleRatioWaiters.shift()(ratio)
  }

  function isRatio(value) {
    return typeof value === 'number' && value >= 0 && value <= 1
  }

  /** The smaller of the two cut sizes shown in `container`, or null. */
  function readCutlePair(container) {
    if (!container) return null
    const labelled = readLabelledCutlePair(container)
    if (labelled !== null) return labelled
    const numbers = firstTwoNumbers(container.textContent || '')
    return numbers ? Math.min(numbers[0], numbers[1]) : null
  }

  function readLabelledCutlePair(container) {
    const leftElement = container.querySelector('.le')
    const cutElement = container.querySelector('.cut')
    if (!leftElement || !cutElement) return null
    const left = numberIn(leftElement.textContent)
    const cut = numberIn(cutElement.textContent)
    return Number.isNaN(left) || Number.isNaN(cut) ? null : Math.min(left, cut)
  }

  function numberIn(text) {
    return Number((text || '').replace(NON_NUMBER_CHARS, '').trim())
  }

  function firstTwoNumbers(text) {
    if (!text) return null
    const matches = text.match(DECIMAL_NUMBERS)
    if (!matches || matches.length < 2) return null
    return [Number(matches[0]), Number(matches[1])]
  }

  /**
   * Prefers the daily best the game stores (keyed by UTC date) the moment the
   * final screen mounts, because the on-screen number runs a ~1s count-up
   * animation. Falls back to the final-screen total (out of 500, genuinely
   * fractional, so not integerOnly).
   */
  function readBlindCutScore(doc) {
    const storedBest = readBlindCutStoredBest()
    if (storedBest !== null) return storedBest
    const finalScore = doc.querySelector(BLINDCUT_FINAL_SCORE)
    return finalScore ? readNumber(finalScore.textContent) : null
  }

  function readBlindCutStoredBest() {
    try {
      const stored = window.localStorage.getItem(BLINDCUT_BEST_KEY_PREFIX + todayUtc())
      if (stored === null) return null
      const best = Number(stored)
      return Number.isFinite(best) ? Math.round(best * HUNDREDTHS) / HUNDREDTHS : null
    } catch (storageUnavailable) {
      return null
    }
  }

  /**
   * The final-screen total (out of 500): its aria-label carries the exact
   * total at once, while the <strong> runs a count-up animation. Falls back
   * to today's recorded completion of the active game.
   */
  function readMotionPathScore(doc) {
    const finalScore = readMotionPathFinalScreen(doc)
    return finalScore !== null ? finalScore : readMotionPathStoredCompletion()
  }

  function readMotionPathFinalScreen(doc) {
    const scoreElement = doc.querySelector(MOTIONPATH_FINAL_SCORE)
    if (!scoreElement) return null
    const labelScore = readNumber(scoreElement.getAttribute('aria-label') || '')
    if (labelScore !== null) return labelScore
    const strongElement = scoreElement.querySelector('strong')
    return strongElement ? readNumber(strongElement.textContent) : null
  }

  function readMotionPathStoredCompletion() {
    try {
      const raw = window.localStorage.getItem(motionPathDailyKey())
      if (!raw) return null
      const state = JSON.parse(raw)
      const entry = state && state.completions && state.completions[todayUtc()]
      return entry && isFiniteNumber(entry.score) ? entry.score : null
    } catch (unreadableState) {
      return null
    }
  }

  /** MotionPath hosts four games, selected by ?game=, each with its own daily state. */
  function motionPathDailyKey() {
    const game = new URL(location.href).searchParams.get(MOTIONPATH_GAME_PARAM) || MOTIONPATH_DEFAULT_GAME
    return MOTIONPATH_DAILY_KEYS[game] || MOTIONPATH_DAILY_KEYS[MOTIONPATH_DEFAULT_GAME]
  }

  function readFirstScore(doc, selectors, readElement) {
    for (const selector of selectors) {
      const element = doc.querySelector(selector)
      const score = element ? readElement(element) : null
      if (score !== null) return score
    }
    return null
  }

  function isFiniteNumber(value) {
    return typeof value === 'number' && Number.isFinite(value)
  }

  function todayUtc() {
    return new Date().toISOString().slice(0, ISO_DATE_LENGTH)
  }

  function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms))
  }
})(globalThis)
