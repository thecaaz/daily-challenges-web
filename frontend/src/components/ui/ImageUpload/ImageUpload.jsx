import React, { useRef, useState } from 'react'
import { Box, Typography } from '@mui/material'
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

  const handleRemove = () => {
    // Reset the input so picking the same file again fires onChange.
    if (inputRef.current) inputRef.current.value = ''
    setFileName('')
    if (onRemove) onRemove()
  }

  return (
    <div>
      <input ref={inputRef} type="file" accept="image/*" onChange={handleChange} style={{ display: 'none' }} />
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
        <AppButton size="small" onClick={() => inputRef.current && inputRef.current.click()}>Browse</AppButton>
        <Typography variant="caption" color="text.secondary">{previewUrl ? (fileName || 'Image attached.') : 'No file selected.'}</Typography>
      </Box>
      <Typography variant="caption" color="text.secondary" component="div" sx={{ mt: 0.75 }}>You can paste an image from clipboard (Ctrl+V).</Typography>
      {previewUrl && (
        <ImagePreview previewUrl={previewUrl} onRemove={handleRemove} />
      )}
    </div>
  )
}
