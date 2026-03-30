import React from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'
import App from './App'
import './styles.css'
import theme from './theme'
import SnackbarProvider from './contexts/SnackbarContext'
import { AuthProvider } from './contexts/AuthContext'

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <ThemeProvider theme={theme}>
        <CssBaseline enableColorScheme />
        <SnackbarProvider>
          <AuthProvider>
            <App />
          </AuthProvider>
        </SnackbarProvider>
      </ThemeProvider>
    </BrowserRouter>
  </React.StrictMode>
)
