import React from 'react'
import { Box, Button, Paper, Typography } from '@mui/material'

// Catches render errors below it and shows a friendly fallback instead of a
// blank page. Pass `resetKey` (e.g. the current pathname) to clear the error
// when the user navigates elsewhere.
export default class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props)
    this.state = { error: null }
  }

  static getDerivedStateFromError(error) {
    return { error }
  }

  componentDidCatch(error, info) {
    console.error('Unhandled UI error', error, info?.componentStack)
  }

  componentDidUpdate(prevProps) {
    if (this.state.error && prevProps.resetKey !== this.props.resetKey) {
      this.setState({ error: null })
    }
  }

  render() {
    if (!this.state.error) return this.props.children

    return (
      <Box role="alert" sx={{ display: 'flex', justifyContent: 'center', mt: 8, px: 2 }}>
        <Paper elevation={0} sx={{ p: { xs: 3, sm: 5 }, maxWidth: 480, textAlign: 'center' }}>
          <Typography variant="h1" sx={{ fontSize: '2.8rem', mb: 1 }}>😵</Typography>
          <Typography variant="h5" component="h1" sx={{ mb: 1 }}>Something went wrong</Typography>
          <Typography color="text.secondary" sx={{ mb: 3 }}>
            This page hit an unexpected error. Reloading usually fixes it.
          </Typography>
          <Button variant="contained" onClick={() => window.location.reload()}>Reload page</Button>
        </Paper>
      </Box>
    )
  }
}
