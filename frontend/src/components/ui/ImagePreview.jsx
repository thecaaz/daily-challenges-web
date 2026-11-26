import React from 'react'
import AppButton from './AppButton'

const PREVIEW_MAX_WIDTH = 320

export default function ImagePreview({ previewUrl, onRemove }) {
  if (!previewUrl) return null

  return (
    <div style={{ marginTop: 8 }}>
      <img src={previewUrl} alt="preview" style={{ maxWidth: PREVIEW_MAX_WIDTH, display: 'block', marginBottom: 6 }} />
      <AppButton variant="text" onClick={onRemove} size="small">Remove</AppButton>
    </div>
  )
}
