import React, { useRef, useEffect, useState } from 'react'
import { Box, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import { useScoreCapture } from '../hooks/useScoreCapture'

const GAME_URL = 'https://timeguessr.com/roundonedaily'

export default function TimeguessrDebug() {
  const iframeRef = useRef(null)
  const { score, capturedFile, submitScore, canCapture, capturing, captureError } = useScoreCapture(iframeRef, { url: GAME_URL })

  // Derive a preview URL from the captured File for display
  const [previewUrl, setPreviewUrl] = useState(null)
  useEffect(() => {
    if (!capturedFile) { setPreviewUrl(null); return }
    const url = URL.createObjectURL(capturedFile)
    setPreviewUrl(url)
    return () => URL.revokeObjectURL(url)
  }, [capturedFile])

  return (
    <Box sx={{ mt: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Typography variant="h5">Debug Timeguessr Test</Typography>
        <AppButton onClick={submitScore} disabled={capturing}>Submit score</AppButton>
        {!canCapture && <Typography variant="body2" color="text.secondary">ScoreBridge extension not detected</Typography>}
      </Box>
      <Box sx={{ mb: 2, width: '100vw', marginLeft: 'calc(50% - 50vw)' }}>
        <iframe
          ref={iframeRef}
          src={GAME_URL}
          style={{ width: '100%', height: 800, border: 0 }}
          title="Timeguessr"
        />
      </Box>
      {captureError && <Typography color="error" sx={{ mt: 1 }}>Capture failed: {captureError}</Typography>}
      {score && <pre style={{ whiteSpace: 'pre-wrap', marginTop: 12 }}>{score}</pre>}
      {previewUrl && <img src={previewUrl} alt="capture preview" style={{ maxWidth: 480, display: 'block', border: '1px solid #ddd', padding: 4, marginTop: 12 }} />}
    </Box>
  )
}
