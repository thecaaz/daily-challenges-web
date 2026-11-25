import React, { useEffect, useState } from 'react'
import AppButton from './AppButton'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { hasAdapterForUrl, subscribeAdapters } from '../../utils/adapters'
import { isSafeHttpUrl } from '../../utils/safeUrl'

/**
 * Plays a game: links into the in-app Play flow only when an extension
 * adapter is known to exist for it, otherwise opens the game's URL in a new
 * tab. Pass `adapter` when it is already known (null for "none"); without it
 * the extension is asked, and asked again when its adapters change.
 */
export default function PlayButton({ game, adapter, variant = 'outlined', size = 'small', color = 'primary', children, ...props }) {
  const safe = isSafeHttpUrl(game?.url)
  const supports = useHasAdapter({ url: game?.url, isSafe: safe, adapter })

  if (!safe) return null

  const label = children || 'Play'
  const endIcon = props.endIcon ?? <OpenInNewIcon />
  const target = supports === true
    ? { to: `/play/${game.id}` }
    : { href: game.url, target: '_blank', rel: 'noopener noreferrer' }

  return (
    <AppButton {...target} variant={variant} size={size} color={color} endIcon={endIcon} {...props}>
      {label}
    </AppButton>
  )
}

function useHasAdapter({ url, isSafe, adapter }) {
  const isAdapterKnown = adapter !== undefined
  const [supports, setSupports] = useState(isAdapterKnown ? Boolean(adapter) : null)
  const adaptersVersion = useAdaptersVersion(!isAdapterKnown)

  useEffect(() => {
    if (isAdapterKnown) {
      setSupports(Boolean(adapter))
      return undefined
    }

    let mounted = true
    const setIfMounted = value => { if (mounted) setSupports(value) }
    if (!isSafe) setIfMounted(false)
    else hasAdapterForUrl(url).then(found => setIfMounted(Boolean(found)), () => setIfMounted(false))
    return () => { mounted = false }
  }, [url, isSafe, adapter, isAdapterKnown, adaptersVersion])

  return supports
}

/** Bumped when the extension's adapters change (e.g. it showed up late). */
function useAdaptersVersion(isSubscribed) {
  const [version, setVersion] = useState(0)

  useEffect(() => {
    if (!isSubscribed) return undefined
    return subscribeAdapters(() => setVersion(v => v + 1))
  }, [isSubscribed])

  return version
}
