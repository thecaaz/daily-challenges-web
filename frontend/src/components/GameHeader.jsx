import React from 'react'
import { CardMedia, Box, Typography } from '@mui/material'
import AppButton from './ui/AppButton'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { getApiRoot } from '../api'

export default function GameHeader({ game }) {
  if (!game) return null

  return (
    <div className="game-header" style={{ marginBottom: 16 }}>
      {game.imageUrl ? (
        <CardMedia
          component="img"
          image={`${getApiRoot()}${game.imageUrl}`}
          alt={game.name}
          loading="lazy"
          className="game-image"
          style={{ width: '100%', height: 260, objectFit: 'cover' }}
        />
      ) : (
        <div className="game-header__fallback" style={{ height: 260 }} />
      )}
      <div className="game-header__overlay">
        <Box className="game-header__content" sx={{ display: 'flex', flexDirection: 'column', gap: 1, maxWidth: '70ch' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
            <Typography variant="h5" sx={{ color: 'white' }}>Submissions — {game.name}</Typography>
          </Box>

          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
            {game.url && (
              <AppButton
                href={game.url}
                target="_blank"
                rel="noopener noreferrer"
                variant="outlined"
                size="small"
                color="primary"
                endIcon={<OpenInNewIcon />}
                dataTest="game-play-link"
                sx={{ textTransform: 'none' }}
              >
                Play
              </AppButton>
            )}
            <AppButton
              to={`/games/${game.id}/highscore`}
              variant="outlined"
              size="small"
              color="primary"
              sx={{ textTransform: 'none' }}
              dataTest="game-highscores-link"
            >
              Highscores
            </AppButton>
            <AppButton
              to={`/games/${game.id}/personal-highscore`}
              variant="outlined"
              size="small"
              color="primary"
              sx={{ textTransform: 'none' }}
              dataTest="game-personal-highscores-link"
            >
              Your Highscores
            </AppButton>
          </Box>

          <div className="muted" style={{ color: 'rgba(255,255,255,0.9)' }}>Compete on daily challenges — climb the leaderboard!</div>
        </Box>
      </div>
    </div>
  )
}
