import React from 'react'
import { Link } from 'react-router-dom'
import { Card, CardContent, CardMedia, CardActionArea, CardActions, Typography, Box, Tooltip } from '@mui/material'
import AppButton from '../AppButton'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import PlayButton from '../PlayButton'
import CheckIcon from '@mui/icons-material/Check'
import ExtensionIcon from '@mui/icons-material/Extension'
import FavoriteButton from '../FavoriteButton'
import useAdapter from '../../../hooks/useAdapter'
import imageUrl from '../../../utils/imageUrl'
import { placeholderGradient } from '../../../styles/tokens'

/**
 * A game tile linking to the game. The favourite button sits in the card
 * actions, outside the CardActionArea link: a button can't be nested in an <a>.
 */
export default function GameCard({ game }) {
  const adapter = useAdapter(game?.url)

  return (
    <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
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
              background: placeholderGradient(0.12),
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
          </Box>
        </CardContent>
      </CardActionArea>

      <CardActions disableSpacing sx={{ px: 2, pb: 2, pt: 0, gap: 1 }}>
        <SubmitScoreButton game={game} />
        {game.url && (
          <PlayButton game={game} adapter={adapter} />
        )}
        <Box sx={{ ml: 'auto' }}>
          <FavoriteButton game={game} />
        </Box>
      </CardActions>
    </Card>
  )
}

function SubmitScoreButton({ game }) {
  const hasSubmitted = Boolean(game.hasSubmittedForLatest)
  const label = hasSubmitted ? 'Already submitted today' : 'Submit score'

  return (
    <AppButton
      to={hasSubmitted ? undefined : `/submit/${game.id}`}
      variant="contained"
      color="primary"
      size="small"
      startIcon={<EmojiEventsIcon />}
      disabled={hasSubmitted}
      aria-label={label}
    >
      {hasSubmitted ? 'Submitted' : 'Submit Score'}
    </AppButton>
  )
}
