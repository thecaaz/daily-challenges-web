import { describe, it, expect, beforeAll, afterAll } from 'vitest'
import loadExtensionScript from './loadExtensionScript'

const INJECTOR_ID = 'cutle-precise-ratio-injector'

describe('content script on a Cutle page', () => {
  beforeAll(() => {
    globalThis.browser = { runtime: { sendMessage: () => {} } }
    window.happyDOM.setURL('https://pfiffel.com/cutle/')
    loadExtensionScript('app-origins.js')
    loadExtensionScript('scoreparse.js')
  })

  afterAll(() => {
    delete globalThis.browser
  })

  it('injects the preciseRatio page script once', () => {
    loadExtensionScript('content-script.js')
    loadExtensionScript('content-script.js')

    const injectors = document.querySelectorAll(`#${INJECTOR_ID}`)
    expect(injectors).toHaveLength(1)
    expect(injectors[0].tagName).toBe('SCRIPT')
    expect(injectors[0].parentNode).toBe(document.head)
    expect(injectors[0].textContent).toContain("ev.data.type !== 'CUTLE_REQUEST_RATIO'")
    expect(injectors[0].textContent).toContain("type: 'CUTLE_PRECISE_RATIO'")
  })
})
