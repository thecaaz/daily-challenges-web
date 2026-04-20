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

  const button = (
    <IconButton size={size} onClick={handleClick} disabled={loading} aria-label="toggle-favorite" sx={sx} {...props}>
      {icon}
    </IconButton>
  )

  return tooltip ? (
    <Tooltip title={isFavorite ? 'Unfavorite' : 'Add to favorites'}>
      {button}
    </Tooltip>
  ) : button
}
