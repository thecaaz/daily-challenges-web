import React, { useEffect, useState } from 'react'
import { Box, CircularProgress, IconButton, Typography } from '@mui/material'
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit'
import { isSafeHttpUrl } from '../../utils/safeUrl'

// Games may run scripts, keep their own origin (storage/cookies), submit forms,
// open popups, show alert/confirm dialogs and download files (e.g. a result
// image), but can't navigate the app's top window.
const IFRAME_SANDBOX = 'allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox allow-modals allow-downloads'

export default function GameIframePlayer({ src, title, iframeRef, isMaximized, onRestore }) {
  const [iframeLoaded, setIframeLoaded] = useState(false)

  useEffect(() => {
    setIframeLoaded(false)
  }, [src])

  if (!isSafeHttpUrl(src)) {
    return <Typography sx={{ mt: 2 }}>This game's URL can't be embedded.</Typography>
  }

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
          aria-label="Restore"
          size="small"
          sx={{ position: 'absolute', top: 8, right: 8, zIndex: 2, bgcolor: 'background.paper', '&:hover': { bgcolor: 'background.paper' } }}
        >
          <FullscreenExitIcon />
        </IconButton>
      )}

      <iframe
        allowFullScreen
        allow="fullscreen"
        sandbox={IFRAME_SANDBOX}
        // Send only our origin: some games check which site embeds them.
        referrerPolicy="strict-origin-when-cross-origin"
        ref={iframeRef}
        src={src}
        title={title || 'Play'}
        onLoad={() => setIframeLoaded(true)}
        style={{ width: '100%', height: '100%', border: 0, visibility: iframeLoaded ? 'visible' : 'hidden', position: 'relative', zIndex: 1 }}
      />
    </Box>
  )
}
