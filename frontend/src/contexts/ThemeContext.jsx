import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { ThemeProvider as MuiThemeProvider } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'
import GlobalStyles from '@mui/material/GlobalStyles'
import createAppTheme from '../theme'
import { cssVariables } from '../styles/tokens'

const ThemeContext = createContext()

const STORAGE_KEY = 'themeMode'
const PREFERS_DARK_QUERY = '(prefers-color-scheme: dark)'
const ThemeMode = Object.freeze({ LIGHT: 'light', DARK: 'dark' })

const ignoreStorageUnavailable = () => {}

function readInitialMode() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored === ThemeMode.LIGHT || stored === ThemeMode.DARK) return stored
    if (window.matchMedia && window.matchMedia(PREFERS_DARK_QUERY).matches) return ThemeMode.DARK
  } catch (error) {
    ignoreStorageUnavailable(error)
  }
  return ThemeMode.LIGHT
}

function applyMode(mode) {
  try {
    document.documentElement.setAttribute('data-theme', mode)
    localStorage.setItem(STORAGE_KEY, mode)
  } catch (error) {
    ignoreStorageUnavailable(error)
  }
}

const oppositeMode = mode => (mode === ThemeMode.DARK ? ThemeMode.LIGHT : ThemeMode.DARK)

export function ThemeProvider({ children }) {
  const [mode, setMode] = useState(readInitialMode)

  useEffect(() => applyMode(mode), [mode])

  const toggleMode = useCallback(() => setMode(oppositeMode), [])

  const theme = useMemo(() => createAppTheme(mode), [mode])
  const globalStyles = useMemo(() => ({ ':root': cssVariables(mode) }), [mode])
  const value = useMemo(() => ({ mode, toggleMode }), [mode, toggleMode])

  return (
    <ThemeContext.Provider value={value}>
      <MuiThemeProvider theme={theme}>
        <CssBaseline enableColorScheme />
        <GlobalStyles styles={globalStyles} />
        {children}
      </MuiThemeProvider>
    </ThemeContext.Provider>
  )
}

export const useThemeMode = () => useContext(ThemeContext)

export default ThemeContext
