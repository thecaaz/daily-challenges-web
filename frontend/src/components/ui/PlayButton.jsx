import React, { useEffect, useState } from 'react'
import AppButton from './AppButton'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { hasAdapterForUrl, subscribeAdapters } from '../../utils/adapters'
import { isSafeHttpUrl } from '../../utils/safeUrl'

export default function PlayButton({ game, adapter, variant = 'outlined', size = 'small', color = 'primary', children, ...props }) {
  const safe = isSafeHttpUrl(game?.url)
  const [supports, setSupports] = useState(adapter !== undefined ? Boolean(adapter) : null)
  // Bumped when the extension's adapters change (e.g. it showed up late).
  const [adaptersVersion, setAdaptersVersion] = useState(0)

  useEffect(() => {
    if (adapter !== undefined) return undefined
    return subscribeAdapters(() => setAdaptersVersion(v => v + 1))
  }, [adapter])

  useEffect(() => {
    let mounted = true
    if (adapter !== undefined) {
      if (mounted) setSupports(Boolean(adapter))
      return () => { mounted = false }
    }

    async function check() {
      if (!safe) {
        if (mounted) setSupports(false)
        return
      }
      try {
        const ok = await hasAdapterForUrl(game.url)
        if (mounted) setSupports(Boolean(ok))
      } catch (e) {
        if (mounted) setSupports(false)
      }
    }
    check()
    return () => { mounted = false }
  }, [game?.url, safe, adapter, adaptersVersion])

  if (!safe) return null

  const label = children || 'Play'
  const endIcon = props.endIcon ?? <OpenInNewIcon />

  // Only link into the in-app Play flow when we know an adapter exists.
  if (supports === true) {
    return (
      <AppButton to={`/play/${game.id}`} variant={variant} size={size} color={color} endIcon={endIcon} {...props}>
        {label}
      </AppButton>
    )
  }

  // Fallback: open the game's URL in a new tab
  return (
    <AppButton href={game.url} target="_blank" rel="noopener noreferrer" variant={variant} size={size} color={color} endIcon={endIcon} {...props}>
      {label}
    </AppButton>
  )
}
