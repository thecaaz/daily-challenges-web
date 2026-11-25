import { describe, it, expect, vi, beforeAll, beforeEach, afterEach, afterAll } from 'vitest'
import { loadContentScripts } from './loadExtensionScript'

const APP_URL = 'http://localhost:3000/'
const APP_ORIGIN = 'http://localhost:3000'
const NONCE = 'nonce-0123456789'
const DATA_URL = 'data:image/png;base64,AAAA'

const sendToBackground = vi.fn()

const todayUtc = () => new Date().toISOString().slice(0, 10)
const flushMessages = () => new Promise(resolve => setTimeout(resolve, 0))

function sendMessage(data, { origin = APP_ORIGIN, source = window } = {}) {
  window.dispatchEvent(new MessageEvent('message', { data, origin, source }))
}

function replyTarget() {
  return { postMessage: vi.fn() }
}

async function requestFromApp(data) {
  const source = replyTarget()
  sendMessage(data, { source })
  await vi.waitFor(() => expect(source.postMessage).toHaveBeenCalled())
  return source.postMessage.mock.calls[0]
}

async function scoreOn(url, html = '') {
  window.happyDOM.setURL(url)
  document.body.innerHTML = html
  const [reply] = await requestFromApp({ type: 'GET_SCORE', nonce: NONCE })
  return reply.score
}

beforeAll(() => {
  globalThis.browser = { runtime: { sendMessage: sendToBackground } }
  loadContentScripts()
})

afterAll(() => {
  delete globalThis.browser
})

beforeEach(() => {
  window.happyDOM.setURL(APP_URL)
  document.body.innerHTML = ''
  window.localStorage.clear()
})

afterEach(() => {
  vi.restoreAllMocks()
  sendToBackground.mockReset()
})

describe('content script GET_ADAPTERS', () => {
  it('lists every built-in adapter with its match descriptor', async () => {
    const [reply, targetOrigin] = await requestFromApp({ type: 'GET_ADAPTERS', nonce: NONCE })

    expect(targetOrigin).toBe(APP_ORIGIN)
    expect(reply).toEqual({
      type: 'ADAPTERS_RESPONSE',
      nonce: NONCE,
      adapters: [
        { name: 'TimeGuessr', matchDescriptor: { type: 'includes', value: 'timeguessr' } },
        { name: 'MapTap', matchDescriptor: { type: 'includes', value: 'maptap' } },
        { name: 'Cutle', matchDescriptor: { type: 'includes', value: 'pfiffel' } },
        { name: 'BlindCut', matchDescriptor: { type: 'includes', value: 'blindcut' } },
        { name: 'MotionPath', matchDescriptor: { type: 'includes', value: 'motionpath' } },
      ],
    })
  })

  it('does not answer a page that is not an app origin', async () => {
    const source = replyTarget()

    sendMessage({ type: 'GET_ADAPTERS', nonce: NONCE }, { origin: 'https://evil.example', source })
    await flushMessages()

    expect(source.postMessage).not.toHaveBeenCalled()
  })
})

describe('content script HAS_ADAPTER', () => {
  it.each([
    ['https://timeguessr.com/game', true, 'TimeGuessr'],
    ['https://MAPTAP.gg/', true, 'MapTap'],
    ['blindcut.app', true, 'BlindCut'],
    ['https://example.com/', false, null],
    ['', false, null],
  ])('answers for %j', async (url, exists, adapterName) => {
    const [reply] = await requestFromApp({ type: 'HAS_ADAPTER', url, nonce: NONCE })

    expect(reply).toEqual({ type: 'HAS_ADAPTER_RESPONSE', nonce: NONCE, exists, adapterName })
  })
})

describe('content script GET_SCORE', () => {
  it('answers null on a page without an adapter', async () => {
    expect(await scoreOn(APP_URL)).toBeNull()
  })

  it('does not answer a request from a page that is not an app origin', async () => {
    window.happyDOM.setURL('https://maptap.gg/')
    const source = replyTarget()

    sendMessage({ type: 'GET_SCORE', nonce: NONCE }, { origin: 'https://evil.example', source })
    await flushMessages()

    expect(source.postMessage).not.toHaveBeenCalled()
  })

  it('answers null and logs when the adapter throws', async () => {
    const logError = vi.spyOn(console, 'error').mockImplementation(() => {})
    window.happyDOM.setURL('https://maptap.gg/')
    vi.spyOn(document, 'querySelector').mockImplementation(() => { throw new Error('boom') })

    const [reply] = await requestFromApp({ type: 'GET_SCORE', nonce: NONCE })

    expect(reply).toEqual({ type: 'SCORE_RESPONSE', nonce: NONCE, score: null })
    expect(logError).toHaveBeenCalled()
  })

  describe('MapTap', () => {
    it('reads #ui_score', async () => {
      expect(await scoreOn('https://maptap.gg/', '<div id="ui_score">Score: 1,234</div>')).toBe(1234)
    })

    it('answers null without a score element', async () => {
      expect(await scoreOn('https://maptap.gg/')).toBeNull()
    })
  })

  describe('TimeGuessr', () => {
    it('reads the summary score as a whole number and opens the breakdown', async () => {
      const html = '<div class="summary-score">40.456</div><button id="breakdownButton"></button>'
      window.happyDOM.setURL('https://timeguessr.com/')
      document.body.innerHTML = html
      const openBreakdown = vi.fn()
      document.querySelector('#breakdownButton').addEventListener('click', openBreakdown)

      const [reply] = await requestFromApp({ type: 'GET_SCORE', nonce: NONCE })

      expect(reply.score).toBe(40456)
      expect(openBreakdown).toHaveBeenCalledTimes(1)
    })

    it('treats a left single quote as a grouping separator', async () => {
      expect(await scoreOn('https://timeguessr.com/', '<div class="summary-score">40‘456</div>')).toBe(40456)
    })

    it('reads again shortly after opening the breakdown when the score was missing', async () => {
      window.happyDOM.setURL('https://timeguessr.com/')
      document.body.innerHTML = '<button id="breakdownButton"></button>'
      document.querySelector('#breakdownButton').addEventListener('click', () => {
        document.body.insertAdjacentHTML('beforeend', '<div class="summary-score">12,000</div>')
      })

      const [reply] = await requestFromApp({ type: 'GET_SCORE', nonce: NONCE })

      expect(reply.score).toBe(12000)
    })
  })

  describe('Cutle', () => {
    const CUTLE_URL = 'https://pfiffel.com/cutle/'

    function answerRatioRequests(ratio) {
      const postMessage = window.postMessage.bind(window)
      vi.spyOn(window, 'postMessage').mockImplementation((message, targetOrigin) => {
        if (message?.type !== 'CUTLE_REQUEST_RATIO') return postMessage(message, targetOrigin)
        expect(targetOrigin).toBe(location.origin)
        sendMessage({ type: 'CUTLE_PRECISE_RATIO', ratio }, { origin: location.origin })
      })
    }

    it('scores the smaller side of the page ratio as a percentage', async () => {
      answerRatioRequests(0.61234)

      expect(await scoreOn(CUTLE_URL)).toBe(38.77)
    })

    it('falls back to the .le/.cut pair in #result when the ratio is invalid', async () => {
      answerRatioRequests(1.5)
      const html = '<div id="result"><span class="le">57.5%</span><span class="cut">42.5%</span></div>'

      expect(await scoreOn(CUTLE_URL, html)).toBe(42.5)
    })

    it('falls back to the first two numbers in #statsScore when the page script does not answer', async () => {
      vi.spyOn(window, 'postMessage').mockImplementation(() => {})

      expect(await scoreOn(CUTLE_URL, '<div id="statsScore">Best 48.2 / 51.8</div>')).toBe(48.2)
    })

    it('answers null without ratio or result', async () => {
      answerRatioRequests(null)

      expect(await scoreOn(CUTLE_URL, '<div id="statsScore">only 1 number</div>')).toBeNull()
    })
  })

  describe('BlindCut', () => {
    it("prefers today's best from localStorage, rounded to hundredths", async () => {
      window.localStorage.setItem(`blind-cut-daily-best:${todayUtc()}`, '437.256')
      const html = '<div class="final-screen"><div class="final-score"><strong>1</strong></div></div>'

      expect(await scoreOn('https://blindcut.app/', html)).toBe(437.26)
    })

    it('falls back to the final screen total', async () => {
      const html = '<div class="final-screen"><div class="final-score"><strong>437.25</strong></div></div>'

      expect(await scoreOn('https://blindcut.app/', html)).toBe(437.25)
    })
  })

  describe('MotionPath', () => {
    const finalScore = (label, strong) =>
      `<div class="final-screen"><div class="final-score" aria-label="${label}"><strong>${strong}</strong></div></div>`

    it('reads the exact total from the aria-label', async () => {
      expect(await scoreOn('https://motionpath.app/', finalScore('Score 412 out of 500', '3'))).toBe(412)
    })

    it('falls back to the animated <strong> total', async () => {
      expect(await scoreOn('https://motionpath.app/', finalScore('', '388'))).toBe(388)
    })

    it("falls back to today's recorded completion of the game selected by ?game=", async () => {
      const completions = { [todayUtc()]: { score: 321 } }
      window.localStorage.setItem('dot-memory.daily.v1', JSON.stringify({ completions }))

      expect(await scoreOn('https://motionpath.app/?game=dots')).toBe(321)
    })

    it('uses the path game for an unknown ?game=', async () => {
      const completions = { [todayUtc()]: { score: 250 } }
      window.localStorage.setItem('motion-path.daily.v1', JSON.stringify({ completions }))

      expect(await scoreOn('https://motionpath.app/?game=unknown')).toBe(250)
    })

    it('answers null for unreadable stored state', async () => {
      window.localStorage.setItem('motion-path.daily.v1', '{not json')

      expect(await scoreOn('https://motionpath.app/')).toBeNull()
    })
  })
})

describe('content script capture', () => {
  let iframe
  let postToApp

  beforeEach(() => {
    iframe = document.createElement('iframe')
    document.body.appendChild(iframe)
    vi.spyOn(iframe, 'getBoundingClientRect').mockReturnValue({ left: 10, top: 20, width: 640, height: 480 })
    postToApp = vi.spyOn(window, 'postMessage').mockImplementation(() => {})
  })

  function arm(nonce = NONCE) {
    sendMessage({ type: 'SCOREBRIDGE_ARM', nonce })
  }

  function requestCapture({ nonce = NONCE, rect = { width: 600, height: 500 }, source = iframe.contentWindow } = {}) {
    sendMessage({ type: 'SCOREBRIDGE_VISIBLE_TAB_CAPTURE', nonce, rect }, { origin: 'https://maptap.gg', source })
  }

  async function captureResponse() {
    await vi.waitFor(() => expect(postToApp).toHaveBeenCalled())
    return postToApp.mock.calls[0]
  }

  it('captures the iframe area of an armed request and posts the image to its own window', async () => {
    sendToBackground.mockResolvedValue({ ok: true, dataUrl: DATA_URL, cropped: true })

    arm()
    requestCapture()

    const expectedRect = { x: 10, y: 20, width: 600, height: 480 }
    expect(await captureResponse()).toEqual([
      { type: 'CAPTURE_RESPONSE', nonce: NONCE, dataUrl: DATA_URL, cropped: true, rect: expectedRect, dpr: 1 },
      APP_ORIGIN,
    ])
    expect(sendToBackground).toHaveBeenCalledWith({ type: 'CAPTURE_TAB', rect: expectedRect })
  })

  it('ignores a child rect that is not a positive number', async () => {
    sendToBackground.mockResolvedValue({ ok: true, dataUrl: DATA_URL })

    arm()
    requestCapture({ rect: { width: -1, height: 'x' } })

    const [response] = await captureResponse()
    expect(response.rect).toEqual({ x: 10, y: 20, width: 640, height: 480 })
    expect(response.cropped).toBe(false)
  })

  it.each([
    ['the background error', { ok: false, error: 'tab is not active' }, 'tab is not active'],
    ['"capture failed" without an image', { ok: true }, 'capture failed'],
    ['"capture failed" without a response', undefined, 'capture failed'],
  ])('reports %s', async (_, backgroundResponse, error) => {
    sendToBackground.mockResolvedValue(backgroundResponse)

    arm()
    requestCapture()

    expect(await captureResponse()).toEqual([{ type: 'CAPTURE_RESPONSE', nonce: NONCE, error }, APP_ORIGIN])
  })

  it('reports a failed background request with its message', async () => {
    sendToBackground.mockRejectedValue(new Error('no receiver'))

    arm()
    requestCapture()

    expect((await captureResponse())[0].error).toBe('no receiver')
  })

  it('refuses a capture that was not armed, and an armed nonce is single use', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    sendToBackground.mockResolvedValue({ ok: true, dataUrl: DATA_URL })

    arm()
    requestCapture()
    await captureResponse()
    requestCapture()
    await flushMessages()

    expect(sendToBackground).toHaveBeenCalledTimes(1)
    expect(warn).toHaveBeenCalledWith('ScoreBridge: ignoring capture request without an armed nonce')
  })

  it('refuses a capture for a different nonce than the armed one', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})

    arm()
    requestCapture({ nonce: 'another-nonce-123' })
    await flushMessages()

    expect(sendToBackground).not.toHaveBeenCalled()
  })

  it('refuses a capture after the arm expired (5 s)', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    const now = Date.now()
    const clock = vi.spyOn(Date, 'now').mockReturnValue(now)

    arm()
    clock.mockReturnValue(now + 5001)
    requestCapture()
    await flushMessages()

    expect(sendToBackground).not.toHaveBeenCalled()
  })

  it('ignores arming from another window or with an invalid nonce', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})

    sendMessage({ type: 'SCOREBRIDGE_ARM', nonce: NONCE }, { source: iframe.contentWindow })
    arm('short')
    requestCapture()
    requestCapture({ nonce: 'short' })
    await flushMessages()

    expect(sendToBackground).not.toHaveBeenCalled()
  })

  it('ignores a capture request that does not come from an iframe of the page', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    arm()
    requestCapture({ source: replyTarget() })
    await flushMessages()

    expect(sendToBackground).not.toHaveBeenCalled()
    expect(warn).not.toHaveBeenCalled()
  })

  it('does not forward capture requests from a top frame', async () => {
    const source = replyTarget()

    sendMessage({ type: 'REQUEST_VISIBLE_TAB_FROM_PARENT', nonce: NONCE }, { source })
    await flushMessages()

    expect(source.postMessage).not.toHaveBeenCalled()
    expect(postToApp).not.toHaveBeenCalled()
  })
})
