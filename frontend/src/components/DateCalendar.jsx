import React, { useMemo } from 'react'
import dayjs from 'dayjs'
import { LocalizationProvider, DateCalendar as MuiDateCalendar, PickersDay } from '@mui/x-date-pickers'
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs'

function toKey(day) {
  if (!day) return null
  return day.format ? day.format('YYYY-MM-DD') : dayjs(day).format('YYYY-MM-DD')
}

const selectedDaySx = { bgcolor: 'primary.main', color: 'primary.contrastText' }
/** Outlined in the text colour, so the highlight is visible in both light and dark mode. */
const dayWithScoresSx = { bgcolor: 'transparent', color: 'text.primary', border: '1px solid', borderColor: 'text.secondary', fontWeight: 700 }

function dayHighlightSx(selected, isAvailable) {
  if (selected) return selectedDaySx
  return isAvailable ? dayWithScoresSx : undefined
}

/** sx arrays may hold objects and functions; appends without nesting arrays. */
function appendSx(baseSx, extraSx) {
  if (!baseSx) return extraSx
  return [...(Array.isArray(baseSx) ? baseSx : [baseSx]), extraSx]
}

/**
 * Defined at module scope so MUI doesn't remount every day cell on each render.
 * `availableDates` (a Set of 'YYYY-MM-DD') arrives through slotProps.day.
 */
function CustomDay(props) {
  const { day, selected, sx: propSx, availableDates, ...other } = props
  const key = toKey(day)
  const isAvailable = key ? Boolean(availableDates?.has(key)) : false
  const sx = appendSx(propSx, dayHighlightSx(selected, isAvailable))

  return <PickersDay {...other} day={day} selected={selected} sx={sx} />
}

export default function DateCalendar({ availableDates = [], selectedDate = '', onChange }) {
  const availSet = useMemo(() => new Set(availableDates || []), [availableDates])
  const slotProps = useMemo(() => ({ day: { availableDates: availSet } }), [availSet])

  const handleChange = (date) => {
    if (!date) {
      onChange('')
      return
    }
    onChange(toKey(date))
  }

  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <MuiDateCalendar
        value={selectedDate ? dayjs(selectedDate) : null}
        onChange={handleChange}
        slots={{ day: CustomDay }}
        slotProps={slotProps}
      />
    </LocalizationProvider>
  )
}
