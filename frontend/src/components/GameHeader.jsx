import React from 'react'
import { CardMedia, Box, Typography, Tooltip } from '@mui/material'
import AppButton from './ui/AppButton'
import PlayButton from './ui/PlayButton'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import ExtensionIcon from '@mui/icons-material/Extension'
import FavoriteButton from './ui/FavoriteButton'
import Countdown from './Countdown'
import imageUrl from '../utils/imageUrl'
import useAdapter from '../hooks/useAdapter'

 

export default function GameHeader({ game }) {
  const adapter = useAdapter(game?.url)

  if (!game) return null

  const desc = (game.description ?? '').trim()
  const fallback = 'Compete on daily challenges — climb the leaderboard!'
  const hasDesc = desc.length > 0
  const maxLen = 300
  const isLong = hasDesc && desc.length > maxLen
  const displayText = hasDesc ? (isLong ? `${desc.slice(0, maxLen)}…` : desc) : fallback

  return (
    <div className="game-header" style={{ marginBottom: 16 }}>
      {game.imageUrl ? (
        <CardMedia
          component="img"
          image={imageUrl(game.imageUrl)}
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
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography variant="h5" sx={{ color: 'white', display: 'flex', alignItems: 'center', gap: 1 }}>
                Submissions — {game.name}
              </Typography>
              {adapter && (
                <Tooltip title="Extension Supported" arrow>
                  <ExtensionIcon sx={{ color: 'white' }} fontSize="small" />
                </Tooltip>
              )}
              <FavoriteButton game={game} sx={{ color: 'white' }} />
            </Box>
          </Box>

          <Countdown targetDate={game.currentScoringDayUtcEnd} />

          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
            {game.url && (
              <PlayButton game={game} adapter={adapter} dataTest="game-play-link" sx={{ textTransform: 'none' }} />
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

          {isLong ? (
            <Tooltip title={desc} arrow>
              <div className="muted" style={{ color: 'rgba(255,255,255,0.9)', whiteSpace: 'pre-line', cursor: 'help' }}>{displayText}</div>
            </Tooltip>
          ) : (
            <div className="muted" style={{ color: 'rgba(255,255,255,0.9)', whiteSpace: 'pre-line' }}>{displayText}</div>
          )}
        </Box>
      </div>
    </div>
  )
}
