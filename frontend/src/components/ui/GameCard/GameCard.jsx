import React from 'react'
import { Link } from 'react-router-dom'
import { Card, CardContent, CardMedia, CardActionArea, CardActions, Typography, Box } from '@mui/material'
import AppButton from '../AppButton'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import CheckIcon from '@mui/icons-material/Check'
import imageUrl from '../../../utils/imageUrl'

export default function GameCard({ game, sx, showSubmit = true }) {
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
            <Typography variant="h6" sx={{ lineHeight: 1.3, m: 0, alignSelf: 'center' }}>
              {game.name}
            </Typography>
            {game.hasSubmittedForLatest && (
              <CheckIcon color="primary" fontSize="small" aria-label="submitted" />
            )}
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
          <AppButton href={game.url} target="_blank" rel="noreferrer" variant="outlined" color="primary" size="small" endIcon={<OpenInNewIcon />}>
            Play
          </AppButton>
        )}
      </CardActions>
    </Card>
  )
}
