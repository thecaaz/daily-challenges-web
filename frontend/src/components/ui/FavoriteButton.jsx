import React from 'react'
import { Tooltip, IconButton } from '@mui/material'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import useFavorite from '../../hooks/useFavorite'

export default function FavoriteButton({ game, gameId, initial, size = 'small', stopPropagationOnClick = false, tooltip = true, sx, ...props }) {
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

  const button = (
    <IconButton size={size} onClick={handleClick} disabled={loading} aria-label="toggle-favorite" sx={sx} {...props}>
      {isFavorite ? <StarIcon fontSize="small" /> : <StarBorderIcon fontSize="small" />}
    </IconButton>
  )

  return tooltip ? (
    <Tooltip title={isFavorite ? 'Unfavorite' : 'Add to favorites'}>
      {button}
    </Tooltip>
  ) : button
}
