import React, { createContext, useCallback, useContext, useMemo, useState } from 'react'
import { Snackbar, Alert } from '@mui/material'

const SnackbarContext = createContext({ showSnackbar: (msg, severity) => {} })

const AUTO_HIDE_MS = 4000
const BOTTOM_CENTER = { vertical: 'bottom', horizontal: 'center' }
const CLICKAWAY = 'clickaway'

/**
 * Provides `showSnackbar(message, severity = 'success')`, the app's single
 * place to surface feedback and API errors. `showSnackbar` has a stable
 * identity because many effects list it as a dependency.
 */
export function SnackbarProvider({ children }) {
  const [open, setOpen] = useState(false)
  const [message, setMessage] = useState('')
  const [severity, setSeverity] = useState('success')

  const showSnackbar = useCallback((msg, sev = 'success') => {
    setMessage(msg)
    setSeverity(sev)
    setOpen(true)
  }, [])

  const handleClose = useCallback((_, reason) => {
    if (reason === CLICKAWAY) return
    setOpen(false)
  }, [])

  const value = useMemo(() => ({ showSnackbar }), [showSnackbar])

  return (
    <SnackbarContext.Provider value={value}>
      {children}
      <Snackbar open={open} autoHideDuration={AUTO_HIDE_MS} onClose={handleClose} anchorOrigin={BOTTOM_CENTER}>
        <Alert onClose={handleClose} severity={severity} sx={{ width: '100%' }}>
          {message}
        </Alert>
      </Snackbar>
    </SnackbarContext.Provider>
  )
}

export const useSnackbar = () => useContext(SnackbarContext)

export default SnackbarProvider
