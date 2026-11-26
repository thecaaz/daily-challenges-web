import React, { useEffect, useState } from 'react'
import { Box, CircularProgress, IconButton, Typography } from '@mui/material'
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit'
import { isSafeHttpUrl } from '../../utils/safeUrl'
import { MAXIMIZED_PLAYER_Z_INDEX } from '../../styles/zIndex'

// Games may run scripts, keep their own origin (storage/cookies), submit forms,
// open popups, show alert/confirm dialogs and download files (e.g. a result
// image), but can't navigate the app's top window.
const IFRAME_SANDBOX = 'allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox allow-modals allow-downloads'
/** Send only our origin: some games check which site embeds them. */
const ORIGIN_ONLY_REFERRER = 'strict-origin-when-cross-origin'
/** Stacking order inside the player: the iframe covers the loading overlay once loaded. */
const LAYER = { loadingOverlay: 0, iframe: 1, restoreButton: 2 }

const MAXIMIZED_PLAYER_SX = { position: 'fixed', left: 0, top: 0, width: '100vw', height: '100vh', zIndex: MAXIMIZED_PLAYER_Z_INDEX }
/** Full viewport width, breaking out of the centred page container. */
const INLINE_PLAYER_SX = { width: '100vw', marginLeft: 'calc(50% - 50vw)', height: '80vh', position: 'relative' }
const LOADING_OVERLAY_SX = { position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.paper', zIndex: LAYER.loadingOverlay }
const RESTORE_BUTTON_SX = { position: 'absolute', top: theme => theme.spacing(1), right: theme => theme.spacing(1), zIndex: LAYER.restoreButton, bgcolor: 'background.paper', '&:hover': { bgcolor: 'background.paper' } }

/** Hidden until loaded so the loading overlay shows instead of a blank frame. */
function iframeStyle(isLoaded) {
  return { width: '100%', height: '100%', border: 0, visibility: isLoaded ? 'visible' : 'hidden', position: 'relative', zIndex: LAYER.iframe }
}

export default function GameIframePlayer({ src, title, iframeRef, isMaximized, onRestore }) {
  const [iframeLoaded, setIframeLoaded] = useState(false)

  useEffect(() => {
    setIframeLoaded(false)
  }, [src])

  if (!isSafeHttpUrl(src)) {
    return <Typography sx={{ mt: 2 }}>This game's URL can't be embedded.</Typography>
  }

  const playerSx = isMaximized ? MAXIMIZED_PLAYER_SX : INLINE_PLAYER_SX

  return (
    <Box sx={playerSx}>
      {!iframeLoaded && (
        <Box sx={LOADING_OVERLAY_SX}>
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
          sx={RESTORE_BUTTON_SX}
        >
          <FullscreenExitIcon />
        </IconButton>
      )}

      <iframe
        allowFullScreen
        allow="fullscreen"
        sandbox={IFRAME_SANDBOX}
        referrerPolicy={ORIGIN_ONLY_REFERRER}
        ref={iframeRef}
        src={src}
        title={title || 'Play'}
        onLoad={() => setIframeLoaded(true)}
        style={iframeStyle(iframeLoaded)}
      />
    </Box>
  )
}
