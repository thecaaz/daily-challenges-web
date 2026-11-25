import React from 'react'
import { useLocation } from 'react-router-dom'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import AppButton from '../ui/AppButton'

const BUTTON_STYLE = { variant: 'contained', color: 'primary', size: 'small' }

/** Links to the submit page (keeping the current query), or shows "Submitted" once the user has. */
export default function SubmitScoreButton({ gameId, hasSubmitted }) {
  const location = useLocation()

  if (hasSubmitted) {
    return (
      <AppButton
        {...BUTTON_STYLE}
        component="span"
        disabled
        title="You've already submitted for today"
        startIcon={<EmojiEventsIcon />}
        aria-label="Already submitted today"
      >
        Submitted
      </AppButton>
    )
  }

  return (
    <AppButton
      {...BUTTON_STYLE}
      to={`/submit/${gameId}${location.search || ''}`}
      startIcon={<EmojiEventsIcon />}
      aria-label="Submit score"
    >
      Submit Score
    </AppButton>
  )
}
