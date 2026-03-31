import React from 'react'
import ImagePreview from '../ImagePreview'

export default function ImageUpload({ onFileChange, previewUrl, onRemove }) {
  return (
    <div>
      <input type="file" accept="image/*" onChange={onFileChange} />
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <div style={{ fontSize: 12, color: '#666' }}>You can paste an image from clipboard (Ctrl+V).</div>
      </div>
      {previewUrl && (
        <ImagePreview previewUrl={previewUrl} onRemove={onRemove} />
      )}
    </div>
  )
}
