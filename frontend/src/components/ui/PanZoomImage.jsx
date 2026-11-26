import React from 'react'
import { Box } from '@mui/material'
import AppButton from './AppButton'
import usePanZoom from '../../hooks/usePanZoom'
import { imageFrame } from '../../styles/tokens'

export default function PanZoomImage({ src, alt, height = 500, containerStyle }) {
  const { containerRef, scale, offset, reset } = usePanZoom()

  return (
    <div>
      <div
        ref={containerRef}
        style={{
          width: '100%', height, border: `1px solid ${imageFrame.borderColor}`, borderRadius: 14,
          overflow: 'hidden', position: 'relative', cursor: 'grab', background: imageFrame.background,
          ...containerStyle,
        }}
      >
        <img
          src={src}
          alt={alt}
          style={{
            transform: `translate(${offset.x}px, ${offset.y}px) scale(${scale})`,
            transformOrigin: 'center center',
            userSelect: 'none',
            pointerEvents: 'none',
            position: 'absolute',
            left: '50%',
            top: '50%',
            maxWidth: 'none',
            width: 'auto',
            height: 'auto',
            minWidth: 0,
            minHeight: 0,
            translate: '-50% -50%',
          }}
        />
      </div>
      <Box sx={{ mt: 1, textAlign: 'center' }}>
        <AppButton size="small" onClick={reset}>Reset View</AppButton>
      </Box>
    </div>
  )
}
