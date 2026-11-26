import React from 'react'
import { Tooltip, IconButton } from '@mui/material'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import useFavorite from '../../hooks/useFavorite'

/**
 * A span keeps the tooltip working while the button is disabled.
 * Tooltip attaches its ref and hover handlers to its child, so they must reach the span.
 */
const TooltipAnchor = React.forwardRef(function TooltipAnchor(props, ref) {
  return <span ref={ref} {...props} />
})

export default function FavoriteButton({ game, sx, activeIconSx, inactiveIconSx }) {
  const { isFavorite, toggle, loading } = useFavorite(game?.id, game?.isFavorite ?? false)

  const icon = isFavorite
    ? <StarIcon fontSize="small" sx={activeIconSx} />
    : <StarBorderIcon fontSize="small" sx={inactiveIconSx} />
  const tooltip = isFavorite ? 'Remove from favorites' : 'Add to favorites'

  return (
    <Tooltip title={tooltip}>
      <TooltipAnchor>
        <IconButton
          size="small"
          onClick={() => toggle()}
          disabled={loading}
          aria-label={favoriteLabel(isFavorite, game?.name)}
          aria-pressed={isFavorite}
          sx={sx}
        >
          {icon}
        </IconButton>
      </TooltipAnchor>
    </Tooltip>
  )
}

function favoriteLabel(isFavorite, gameName) {
  if (isFavorite) return gameName ? `Remove ${gameName} from favorites` : 'Remove from favorites'
  return gameName ? `Add ${gameName} to favorites` : 'Add to favorites'
}
