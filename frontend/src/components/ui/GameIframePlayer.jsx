import React, { useEffect, useState } from 'react'
import { Box, CircularProgress, IconButton, Typography } from '@mui/material'
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit'

export default function GameIframePlayer({ src, title, iframeRef, isMaximized, onRestore }) {
  const [iframeLoaded, setIframeLoaded] = useState(false)

  useEffect(() => {
    setIframeLoaded(false)
  }, [src])

  return (
    <Box sx={isMaximized ? { position: 'fixed', left: 0, top: 0, width: '100vw', height: '100vh', zIndex: 2147483646 } : { width: '100vw', marginLeft: 'calc(50% - 50vw)', height: '80vh', position: 'relative' }}>
      {!iframeLoaded && (
        <Box sx={{ position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.paper', zIndex: 0 }}>
          <CircularProgress />
          <Typography sx={{ mt: 2 }}>Loading game...</Typography>
        </Box>
      )}

      {isMaximized && (
        <IconButton
          onClick={onRestore}
          title="Restore"
          size="small"
          sx={{ position: 'absolute', top: 8, right: 8, zIndex: 2, bgcolor: 'background.paper', '&:hover': { bgcolor: 'background.paper' } }}
        >
          <FullscreenExitIcon />
        </IconButton>
      )}

      <iframe
        allowFullScreen
        allow="fullscreen"
        ref={iframeRef}
        src={src}
        title={title || 'Play'}
        onLoad={() => setIframeLoaded(true)}
        style={{ width: '100%', height: '100%', border: 0, visibility: iframeLoaded ? 'visible' : 'hidden', position: 'relative', zIndex: 1 }}
      />
    </Box>
  )
}
