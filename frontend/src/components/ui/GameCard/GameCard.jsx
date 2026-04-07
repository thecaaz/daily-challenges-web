import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Card, CardContent, CardMedia, CardActionArea, CardActions, Typography, Box, Tooltip, IconButton } from '@mui/material'
import AppButton from '../AppButton'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import PlayButton from '../PlayButton'
import CheckIcon from '@mui/icons-material/Check'
import ExtensionIcon from '@mui/icons-material/Extension'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import useFavorite from '../../../hooks/useFavorite'
import { getAdapterForUrl } from '../../../utils/adapters'
import imageUrl from '../../../utils/imageUrl'

function FavoriteButton({ game }) {
  const { isFavorite, toggle, loading } = useFavorite(game.id, game.isFavorite)
  const handleClick = (ev) => {
    ev.preventDefault()
    ev.stopPropagation()
    toggle()
  }

  return (
    <Tooltip title={isFavorite ? 'Unfavorite' : 'Add to favorites'}>
      <IconButton size="small" onClick={handleClick} disabled={loading} aria-label="toggle-favorite">
        {isFavorite ? <StarIcon fontSize="small" /> : <StarBorderIcon fontSize="small" />}
      </IconButton>
    </Tooltip>
  )
}

export default function GameCard({ game, sx, showSubmit = true }) {
  const [adapter, setAdapter] = useState(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      if (!game?.url) return
      try {
        const a = await getAdapterForUrl(game.url)
        if (mounted) setAdapter(a)
      } catch (e) {
        if (mounted) setAdapter(null)
      }
    }
    load()
    return () => { mounted = false }
  }, [game?.url])

  return (
    <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column', ...(sx || {}) }}>
      <CardActionArea component={Link} to={`/games/${game.id}`} sx={{ flexGrow: 1 }}>
        {game.imageUrl ? (
          <CardMedia
            component="img"
            image={imageUrl(game.imageUrl)}
            alt={game.name}
            height={180}
            loading="lazy"
            sx={{ objectFit: 'cover' }}
          />
        ) : (
          <Box
            sx={{
              height: 180,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              background: 'linear-gradient(135deg, rgba(255,122,182,0.12), rgba(255,209,102,0.12))',
              fontSize: '3.5rem',
            }}
          >
            🎮
          </Box>
        )}
        <CardContent sx={{ pb: 1 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography variant="h6" sx={{ lineHeight: 1.3, m: 0, alignSelf: 'center', display: 'flex', alignItems: 'center', gap: 1 }}>
              {game.name}
              {adapter && (
                <Tooltip title={adapter.name ? `Adapter: ${adapter.name}` : 'Extension Supported'} arrow>
                  <ExtensionIcon color="action" fontSize="small" aria-label="extension-supported" />
                </Tooltip>
              )}
            </Typography>
            {game.hasSubmittedForLatest && (
              <CheckIcon color="primary" fontSize="small" aria-label="submitted" />
            )}
            {/* Favorite toggle */}
            <FavoriteButton game={game} />
          </Box>
        </CardContent>
      </CardActionArea>

      <CardActions sx={{ px: 2, pb: 2, pt: 0, gap: 1 }}>
        {showSubmit && (
          (() => {
            const disabled = Boolean(game.hasSubmittedForLatest);
            const to = disabled ? undefined : `/submit/${game.id}`;
            return (
              <AppButton
                to={to}
                variant="contained"
                color="primary"
                size="small"
                startIcon={<EmojiEventsIcon />}
                disabled={disabled}
                aria-label={disabled ? 'Already submitted today' : 'Submit score'}
              >
                {disabled ? 'Submitted' : 'Submit Score'}
              </AppButton>
            )
          })()
        )}
        {game.url && (
          <PlayButton game={game} adapter={adapter} />
        )}
      </CardActions>
    </Card>
  )
}
