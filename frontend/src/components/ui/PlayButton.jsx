import React from 'react'
import AppButton from './AppButton'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { useHasAdapter } from '../../hooks/useAdapter'
import { isSafeHttpUrl } from '../../utils/safeUrl'

/**
 * Plays a game: links into the in-app Play flow only when an extension
 * adapter is known to exist for it, otherwise opens the game's URL in a new
 * tab. Pass `adapter` when it is already known (null for "none"); without it
 * the extension is asked, and asked again when its adapters change.
 */
export default function PlayButton({ game, adapter, variant = 'outlined', size = 'small', color = 'primary', children, ...props }) {
  const safe = isSafeHttpUrl(game?.url)
  const isAdapterKnown = adapter !== undefined
  const hasLookedUpAdapter = useHasAdapter(isAdapterKnown || !safe ? null : game.url)
  const supports = isAdapterKnown ? Boolean(adapter) : hasLookedUpAdapter

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
