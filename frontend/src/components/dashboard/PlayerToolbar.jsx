import React from 'react'
import { Box, IconButton, Tooltip, Typography } from '@mui/material'
import FullscreenIcon from '@mui/icons-material/Fullscreen'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import SkipNextIcon from '@mui/icons-material/SkipNext'
import ExitToAppIcon from '@mui/icons-material/ExitToApp'
import { useNavigate } from 'react-router-dom'
import AppButton from '../ui/AppButton'

/** Title, progress and controls of the favorites player; `player` is useFavoritesPlayer's state. */
export default function PlayerToolbar({ player }) {
  const navigate = useNavigate()
  const { current, currentIndex, queue, currentUrl, isMaximized, setIsMaximized, advance } = player
  const { canCapture, showSubmission, capturing, submitScore } = player.capture
  const isPlaying = Boolean(currentUrl) && !showSubmission
  const canSubmit = isPlaying && canCapture
  const canMaximize = isPlaying && !isMaximized

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
      <Typography variant="h5">{current.name}</Typography>
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
        <Typography sx={{ alignSelf: 'center' }}>{currentIndex + 1} / {queue.length}</Typography>
        {currentUrl && (
          <Tooltip title="Open in new tab">
            <IconButton href={currentUrl} target="_blank" rel="noopener noreferrer" size="small" aria-label="Open in new tab">
              <OpenInNewIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        {canSubmit && (
          <AppButton onClick={submitScore} variant="contained" size="small" disabled={capturing}>
            {capturing ? 'Capturing…' : 'Submit'}
          </AppButton>
        )}
        {canMaximize && (
          <Tooltip title="Maximize">
            <IconButton onClick={() => setIsMaximized(true)} size="small" aria-label="Maximize">
              <FullscreenIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        <Tooltip title="Skip">
          <span>
            <IconButton size="small" onClick={advance} disabled={capturing} aria-label="Skip">
              <SkipNextIcon fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
        <Tooltip title="Quit">
          <IconButton size="small" onClick={() => navigate('/dashboard')} aria-label="Quit">
            <ExitToAppIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>
    </Box>
  )
}
