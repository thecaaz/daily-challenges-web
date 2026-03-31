import React, { useRef, useState } from 'react'
import ImagePreview from '../ImagePreview'
import AppButton from '../AppButton'

export default function ImageUpload({ onFileChange, previewUrl, onRemove }) {
  const inputRef = useRef(null)
  const [fileName, setFileName] = useState('')

  const handleChange = (e) => {
    const file = e.target.files && e.target.files[0]
    setFileName(file ? file.name : '')
    if (onFileChange) onFileChange(e)
  }

  return (
    <div>
      <input ref={inputRef} type="file" accept="image/*" onChange={handleChange} style={{ display: 'none' }} />
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <AppButton size="small" onClick={() => inputRef.current && inputRef.current.click()}>Browse</AppButton>
        <div style={{ fontSize: 12, color: '#666' }}>{fileName || 'No file selected.'}</div>
      </div>
      <div style={{ marginTop: 6, fontSize: 12, color: '#666' }}>You can paste an image from clipboard (Ctrl+V).</div>
      {previewUrl && (
        <ImagePreview previewUrl={previewUrl} onRemove={onRemove} />
      )}
    </div>
  )
}
