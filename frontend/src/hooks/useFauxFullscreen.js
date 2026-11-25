import { useCallback, useRef } from 'react'

const TOPMOST_Z_INDEX = '2147483647'
const FULLSCREEN_STYLE = { position: 'fixed', left: '0', top: '0', width: '100vw', height: '100vh', zIndex: TOPMOST_Z_INDEX }
const OVERRIDDEN_STYLE_KEYS = ['position', 'zIndex', 'left', 'top', 'width', 'height']

/**
 * Stretches the iframe's container over the whole viewport ("faux
 * fullscreen") and puts it back afterwards. Both callbacks are stable.
 *
 * @param {React.RefObject<HTMLIFrameElement>} iframeRef
 * @returns {{ apply: () => boolean, restore: () => void }} `apply` returns
 *   false when there is no container or the overlay is already up.
 */
export default function useFauxFullscreen(iframeRef) {
  const overlayRef = useRef(null)

  const restore = useCallback(() => {
    const overlay = overlayRef.current
    overlayRef.current = null
    if (overlay) Object.assign(overlay.container.style, overlay.previousStyle)
  }, [])

  const apply = useCallback(() => {
    const container = iframeRef.current?.parentElement
    if (!container || overlayRef.current) return false
    overlayRef.current = { container, previousStyle: pickStyle(container.style, OVERRIDDEN_STYLE_KEYS) }
    Object.assign(container.style, FULLSCREEN_STYLE)
    return true
  }, [iframeRef])

  return { apply, restore }
}

function pickStyle(style, keys) {
  return Object.fromEntries(keys.map(key => [key, style[key]]))
}
