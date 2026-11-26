import React, { useState } from 'react'
import { Chip, Tooltip } from '@mui/material'
import useReleaseInfo from '../hooks/useReleaseInfo'
import ChangelogModal from './ChangelogModal'
import { faintOutline } from '../styles/tokens'

export default function VersionBadge() {
  const info = useReleaseInfo()
  const [open, setOpen] = useState(false)

  if (!info || !info.version) return null

  const labelBase = info.version
  const label = labelBase?.toString().startsWith('v') ? labelBase : `v${labelBase}`
  const raw = info.rawVersion || info.version

  return (
    <>
      <Tooltip title={raw} arrow>
        <Chip
          label={label}
          size="small"
          clickable
          onClick={() => setOpen(true)}
          sx={{ ml: 1, bgcolor: 'transparent', border: faintOutline, color: 'inherit' }}
        />
      </Tooltip>
      <ChangelogModal open={open} onClose={() => setOpen(false)} version={labelBase} rawVersion={raw} changelog={info.changelog} />
    </>
  )
}
