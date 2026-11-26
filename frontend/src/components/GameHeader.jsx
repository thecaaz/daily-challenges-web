import React from 'react'
import { CardMedia, Box, Typography, Tooltip } from '@mui/material'
import AppButton from './ui/AppButton'
import PlayButton from './ui/PlayButton'
import ExtensionIcon from '@mui/icons-material/Extension'
import FavoriteButton from './ui/FavoriteButton'
import Countdown from './Countdown'
import imageUrl from '../utils/imageUrl'
import useAdapter from '../hooks/useAdapter'
import { onImageText } from '../styles/tokens'

const FALLBACK_DESCRIPTION = 'Compete on daily challenges — climb the leaderboard!'
const MAX_DESCRIPTION_LENGTH = 300
const descriptionStyle = { color: onImageText.body, whiteSpace: 'pre-line' }
const truncatedDescriptionStyle = { ...descriptionStyle, cursor: 'help' }

/**
 * The text shown under the game title: the trimmed description (cut to
 * MAX_DESCRIPTION_LENGTH characters with an ellipsis) or a fallback.
 */
function truncateDescription(description) {
  const fullText = (description ?? '').trim()
  if (!fullText) return { text: FALLBACK_DESCRIPTION, fullText, isTruncated: false }
  if (fullText.length <= MAX_DESCRIPTION_LENGTH) return { text: fullText, fullText, isTruncated: false }
  return { text: `${fullText.slice(0, MAX_DESCRIPTION_LENGTH)}…`, fullText, isTruncated: true }
}

/**
 * @param {object} props
 * @param {object} props.game
 * @param {() => void} [props.onExpire] - called when the scoring day resets; the parent should refetch the game.
 */
export default function GameHeader({ game, onExpire }) {
  const adapter = useAdapter(game?.url)

  if (!game) return null

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

          <Countdown targetDate={game.currentScoringDayUtcEnd} onExpire={onExpire} />

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

          <GameDescription description={game.description} />
        </Box>
      </div>
    </div>
  )
}

function GameDescription({ description }) {
  const { text, fullText, isTruncated } = truncateDescription(description)
  const descriptionDiv = (
    <div className="muted" style={isTruncated ? truncatedDescriptionStyle : descriptionStyle}>{text}</div>
  )

  if (!isTruncated) return descriptionDiv
  return <Tooltip title={fullText} arrow>{descriptionDiv}</Tooltip>
}
