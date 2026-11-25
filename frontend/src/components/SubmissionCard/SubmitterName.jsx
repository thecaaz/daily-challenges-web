import React from 'react'
import { useNavigate } from 'react-router-dom'
import { Typography } from '@mui/material'
import stopCardNavigation from './stopCardNavigation'

const ANONYMOUS_NAME = 'Anonymous'
const ACTIVATION_KEYS = ['Enter', ' ']

const nameSx = { fontWeight: 700, lineHeight: 1.2 }
const profileLinkSx = { ...nameSx, cursor: 'pointer', color: 'primary.main', textDecoration: 'underline' }

/** Submitter name; links to the profile without triggering the card's own link. */
export default function SubmitterName({ submission }) {
  const navigate = useNavigate()
  const name = submission.username ?? ANONYMOUS_NAME

  if (!submission.userId) {
    return <Typography variant="subtitle1" sx={nameSx}>{name}</Typography>
  }

  const openProfile = event => {
    stopCardNavigation(event)
    navigate(`/users/${submission.userId}`)
  }
  const handleKeyDown = event => {
    if (ACTIVATION_KEYS.includes(event.key)) openProfile(event)
  }

  return (
    <Typography
      component="span"
      variant="subtitle1"
      sx={profileLinkSx}
      onClick={openProfile}
      onKeyDown={handleKeyDown}
      tabIndex={0}
      role="link"
    >
      {name}
    </Typography>
  )
}
