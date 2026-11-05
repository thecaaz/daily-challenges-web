import React from 'react'
import { Tooltip, IconButton } from '@mui/material'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import useFavorite from '../../hooks/useFavorite'

export default function FavoriteButton({
  game,
  gameId,
  initial,
  size = 'small',
  stopPropagationOnClick = false,
  tooltip = true,
  sx,
  activeIconSx,
  inactiveIconSx,
  ...props
}) {
  const id = game?.id ?? gameId
  const initialValue = initial !== undefined ? initial : (game?.isFavorite ?? false)
  const { isFavorite, toggle, loading } = useFavorite(id, initialValue)

  const handleClick = (ev) => {
    if (stopPropagationOnClick && ev) {
      ev.preventDefault()
      ev.stopPropagation()
    }
    toggle()
  }

  const icon = isFavorite
    ? <StarIcon fontSize="small" sx={activeIconSx} />
    : <StarBorderIcon fontSize="small" sx={inactiveIconSx} />

  const name = game?.name
  const label = isFavorite
    ? (name ? `Remove ${name} from favorites` : 'Remove from favorites')
    : (name ? `Add ${name} to favorites` : 'Add to favorites')

  const button = (
    <IconButton
      size={size}
      onClick={handleClick}
      disabled={loading}
      aria-label={label}
      aria-pressed={isFavorite}
      sx={sx}
      {...props}
    >
      {icon}
    </IconButton>
  )

  return tooltip ? (
    <Tooltip title={isFavorite ? 'Remove from favorites' : 'Add to favorites'}>
      {/* span keeps the tooltip working while the button is disabled */}
      <span>{button}</span>
    </Tooltip>
  ) : button
}
