import React from 'react'
import { Button } from '@mui/material'

export default function ImagePreview({ previewUrl, onRemove, alt = 'preview', maxWidth = 320 }) {
  if (!previewUrl) return null

  return (
    <div style={{ marginTop: 8 }}>
      <img src={previewUrl} alt={alt} style={{ maxWidth, display: 'block', marginBottom: 6 }} />
      <Button onClick={onRemove} size="small">Remove</Button>
    </div>
  )
}
