import React, { createContext, useCallback, useContext, useMemo, useState } from 'react'
import { Snackbar, Alert } from '@mui/material'

const SnackbarContext = createContext({ showSnackbar: (msg, severity) => {} })

export function SnackbarProvider({ children }) {
  const [open, setOpen] = useState(false)
  const [message, setMessage] = useState('')
  const [severity, setSeverity] = useState('success')

  // Stable identity: many effects list showSnackbar as a dependency.
  const showSnackbar = useCallback((msg, sev = 'success') => {
    setMessage(msg)
    setSeverity(sev)
    setOpen(true)
  }, [])

  const handleClose = useCallback((_, reason) => {
    if (reason === 'clickaway') return
    setOpen(false)
  }, [])

  const value = useMemo(() => ({ showSnackbar }), [showSnackbar])

  return (
    <SnackbarContext.Provider value={value}>
      {children}
      <Snackbar open={open} autoHideDuration={4000} onClose={handleClose} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert onClose={handleClose} severity={severity} sx={{ width: '100%' }}>
          {message}
        </Alert>
      </Snackbar>
    </SnackbarContext.Provider>
  )
}

export const useSnackbar = () => useContext(SnackbarContext)

export default SnackbarProvider
