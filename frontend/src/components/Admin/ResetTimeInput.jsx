import React from 'react'
import { Typography } from '@mui/material'
import { localToUtcHHmm } from '../../utils/resetTime'

/** A local-time reset time input that also shows the UTC time it is stored as. */
export default function ResetTimeInput({ label, value, onChange }) {
  const utcResetTime = localToUtcHHmm(value)

  return (
    <div>
      <label>{label}: </label>
      <input type="time" value={value} onChange={e => onChange(e.target.value)} required />
      {utcResetTime && (
        <Typography variant="caption" color="text.secondary" sx={{ ml: 1 }}>
          = {utcResetTime} UTC
        </Typography>
      )}
    </div>
  )
}
