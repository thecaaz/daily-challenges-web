import React from 'react'
import { Link } from 'react-router-dom'
import { Card, CardContent, CardMedia, CardActionArea, CardActions, Typography, Button, Box } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { getApiRoot } from '../../../api'

export default function GameCard({ game, sx, showSubmit = true }) {
  const apiRoot = getApiRoot()

  return (
    <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column', ...(sx || {}) }}>
      <CardActionArea component={Link} to={`/games/${game.id}`} sx={{ flexGrow: 1 }}>
        {game.imageUrl ? (
          <CardMedia
            component="img"
            image={`${apiRoot}${game.imageUrl}`}
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
          <Typography variant="h6" gutterBottom sx={{ lineHeight: 1.3 }}>
            {game.name}
          </Typography>
        </CardContent>
      </CardActionArea>

      <CardActions sx={{ px: 2, pb: 2, pt: 0, gap: 1 }}>
        {showSubmit && (
          <Button
            component={Link}
            to={`/submit/${game.id}`}
            variant="contained"
            color="primary"
            size="small"
            startIcon={<EmojiEventsIcon />}
          >
            Submit Score
          </Button>
        )}
        {game.url && (
          <Button
            href={game.url}
            target="_blank"
            rel="noreferrer"
            variant="outlined"
            color="primary"
            size="small"
            endIcon={<OpenInNewIcon />}
          >
            Play
          </Button>
        )}
      </CardActions>
    </Card>
  )
}
