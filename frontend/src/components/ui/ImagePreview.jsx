import React from 'react'
import AppButton from './AppButton'

export default function ImagePreview({ previewUrl, onRemove, alt = 'preview', maxWidth = 320 }) {
  if (!previewUrl) return null

  return (
    <div style={{ marginTop: 8 }}>
      <img src={previewUrl} alt={alt} style={{ maxWidth, display: 'block', marginBottom: 6 }} />
      <AppButton variant="text" onClick={onRemove} size="small">Remove</AppButton>
    </div>
  )
}
