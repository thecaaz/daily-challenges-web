import React, { useState } from 'react'
import { Popover, Box, Typography } from '@mui/material'
import AppButton from './ui/AppButton'
import DateCalendar from './DateCalendar'

export default function DatePickerButton({ availableDates = [], selectedDate = '', onChange }) {
  const [anchorEl, setAnchorEl] = useState(null)

  const handleOpen = (e) => setAnchorEl(e.currentTarget)
  const handleClose = () => setAnchorEl(null)

  const handleSelect = (value) => {
    if (onChange) onChange(value)
    handleClose()
  }

  return (
    <>
      <AppButton onClick={handleOpen} sx={{ height: 40 }}>
        {selectedDate || 'All'}
      </AppButton>

      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'left' }}
        slotProps={{ paper: { sx: { width: 340 } } }}
      >
        <Box sx={{ p: 1, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Day</Typography>
          {selectedDate ? (
            <AppButton variant="text" size="small" onClick={() => { if (onChange) onChange(''); handleClose() }} sx={{ textTransform: 'none', fontSize: '0.75rem' }}>
              All
            </AppButton>
          ) : null}
        </Box>

        <Box sx={{ p: 1 }}>
          <DateCalendar availableDates={availableDates} selectedDate={selectedDate} onChange={handleSelect} />
        </Box>
      </Popover>
    </>
  )
}
