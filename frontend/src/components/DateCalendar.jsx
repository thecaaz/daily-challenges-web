import React, { useMemo } from 'react'
import dayjs from 'dayjs'
import { LocalizationProvider, DateCalendar as MuiDateCalendar, PickersDay } from '@mui/x-date-pickers'
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs'

export default function DateCalendar({ availableDates = [], selectedDate = '', onChange }) {
  const availSet = useMemo(() => new Set(availableDates || []), [availableDates])

  const handleChange = (d) => {
    if (!d) {
      onChange('')
      return
    }
    const formatted = d && d.format ? d.format('YYYY-MM-DD') : dayjs(d).format('YYYY-MM-DD')
    onChange(formatted)
  }

  const CustomDay = (props) => {
    const { day, selected, sx: propSx, ...other } = props
    const s = day ? (day.format ? day.format('YYYY-MM-DD') : dayjs(day).format('YYYY-MM-DD')) : null
    const isAvailable = s ? availSet.has(s) : false

    // Selected day should use primary color; available (scored) days use the
    // same color as the page "Back" button (kept as a hex here to match usage).
    const BACK_BUTTON_COLOR = '#444'
    const computedSx = selected
      ? { bgcolor: 'primary.main', color: 'white' }
      : isAvailable
        ? { bgcolor: 'transparent', color: BACK_BUTTON_COLOR, border: `1px solid ${BACK_BUTTON_COLOR}` }
        : undefined

    // Merge any incoming sx prop provided by MUI with our computed styles
    const mergedSx = propSx
      ? (typeof propSx === 'function' ? (theme) => ({ ...propSx(theme), ...computedSx }) : { ...propSx, ...computedSx })
      : computedSx

    return (
      <PickersDay
        {...other}
        day={day}
        selected={selected}
        sx={mergedSx}
      />
    )
  }

  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <MuiDateCalendar
        value={selectedDate ? dayjs(selectedDate) : null}
        onChange={handleChange}
        slots={{ day: CustomDay }}
      />
    </LocalizationProvider>
  )
}
