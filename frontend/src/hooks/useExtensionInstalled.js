import { useEffect, useState } from 'react'
import { refreshAdapters, isExtensionDetected } from '../utils/adapters'

/**
 * Whether the ScoreBridge extension is installed: null while checking.
 *
 * It always asks again on mount, because the user may have just installed
 * the extension; a content script answering GET_ADAPTERS means it is present.
 *
 * @returns {boolean|null}
 */
export default function useExtensionInstalled() {
  const [isInstalled, setIsInstalled] = useState(null)

  useEffect(() => {
    let isMounted = true
    refreshAdapters()
      .then(() => { if (isMounted) setIsInstalled(isExtensionDetected() === true) })
      .catch(() => { if (isMounted) setIsInstalled(false) })
    return () => { isMounted = false }
  }, [])

  return isInstalled
}
