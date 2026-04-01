import React, { useEffect, useState } from 'react'
import { Chip, Tooltip } from '@mui/material'
import api from '../api'
import ChangelogModal from './ChangelogModal'

export default function VersionBadge() {
  const [info, setInfo] = useState(null)
  const [open, setOpen] = useState(false)

  useEffect(() => {
    let mounted = true
    api.get('/info').then(res => {
      if (!mounted) return
      setInfo(res.data)
    }).catch(() => {})
    return () => { mounted = false }
  }, [])

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
          sx={{ ml: 1, bgcolor: 'transparent', border: '1px solid rgba(255,255,255,0.08)', color: 'inherit' }}
        />
      </Tooltip>
      <ChangelogModal open={open} onClose={() => setOpen(false)} version={labelBase} rawVersion={raw} changelog={info.changelog} />
    </>
  )
}
