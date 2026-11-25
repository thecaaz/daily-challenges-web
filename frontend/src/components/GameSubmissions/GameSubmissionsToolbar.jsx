import React from 'react'
import { Stack, Box } from '@mui/material'
import AppButton from '../ui/AppButton'
import DatePickerButton from '../DatePickerButton'
import SubmitScoreButton from './SubmitScoreButton'

const SECONDARY_BUTTON_SX = { bgcolor: 'background.paper', color: 'text.primary', boxShadow: 'none' }

/**
 * Day picker, compare toggle, back and submit buttons above a game's submissions.
 * `compare` is the result of `useCompareSelection`; the toggle only shows when `canOfferCompare`.
 */
export default function GameSubmissionsToolbar({ game, day, compare, canOfferCompare, hasSubmitted }) {
  return (
    <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <DatePickerButton availableDates={day.availableDates} selectedDate={day.selectedDate} onChange={day.selectDate} />
      </Box>
      <div>
        {canOfferCompare && <CompareToggleButton isComparing={compare.isComparing} onToggle={compare.toggleMode} />}
        <AppButton to="/" sx={{ mr: 1, ...SECONDARY_BUTTON_SX }}>Back</AppButton>
        <SubmitScoreButton gameId={game.id} hasSubmitted={hasSubmitted} />
      </div>
    </Stack>
  )
}

function CompareToggleButton({ isComparing, onToggle }) {
  return (
    <AppButton onClick={onToggle} sx={{ mr: 1, ...(isComparing ? {} : SECONDARY_BUTTON_SX) }}>
      {isComparing ? 'Cancel Compare' : 'Compare'}
    </AppButton>
  )
}
