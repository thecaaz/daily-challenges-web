import React, { useEffect, useState } from 'react'
import { Box, Button, Typography, Paper, List, ListItem, ListItemText, Chip } from '@mui/material'
import ExtensionIcon from '@mui/icons-material/Extension'
import DownloadIcon from '@mui/icons-material/Download'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'

const DOWNLOAD_URL = 'https://addons.mozilla.org/firefox/downloads/file/4760673/2915713b218843aca6f8-0.1.1.xpi'

export default function Extension() {
  const [installed, setInstalled] = useState(null)

  useEffect(() => {
    let mounted = true
    const nonce = `${Date.now()}-${Math.random().toString(36).slice(2)}`

    function onMessage(ev) {
      if (!ev || !ev.data || typeof ev.data.type !== 'string') return
      if (ev.data.type === 'ADAPTERS_RESPONSE' && ev.data.nonce === nonce) {
        if (mounted) {
          debugger;
          setInstalled(true)  
          clearTimeout(timer)        
        } 
      }
    }

    window.addEventListener('message', onMessage)
    try {
      // Ask any injected content script for its adapters; a responding ADAPTERS_RESPONSE
      // implies the extension (or at least a content script) is present on this page.
      window.postMessage({ type: 'GET_ADAPTERS', nonce }, '*')
    } catch (e) {}

    const timer = setTimeout(() => {
      if (mounted && installed !== true) {
        debugger
        setInstalled(false)
      } 
    }, 800)

    return () => {
      mounted = false
      window.removeEventListener('message', onMessage)
      clearTimeout(timer)
    }
  }, [])

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
        <ExtensionIcon fontSize="large" />
        <Typography variant="h4" component="h1">Browser Extension</Typography>
        {installed && (
          <Chip
            color="success"
            icon={<CheckCircleIcon />}
            label="Extension already installed"
            sx={{ ml: 2 }}
          />
        )}
      </Box>

      <Typography variant="body1" sx={{ mb: 3 }}>
        The <strong>DailyChallenges ScoreBridge</strong> Firefox extension bridges embedded game pages
        with the daily-challenges site. It reads your score directly from the game and captures a screenshot
        of the result, so you can submit without typing your score manually.
      </Typography>

      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 'bold', mb: 1 }}>What it does</Typography>
        <List dense disablePadding>
          <ListItem disableGutters>
            <ListItemText primary="Reads your score automatically from supported game pages" />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText primary="Captures a screenshot of your result for submission" />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText primary="Pre-fills the submission form so you only need to confirm" />
          </ListItem>
        </List>
        <Typography variant="subtitle2" sx={{ mt: 1 }}>Supported games</Typography>
        <List dense disablePadding>
          <ListItem disableGutters>
            <ListItemText primary="TimeGuessr" />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText primary="MapTap" />
          </ListItem>
        </List>
      </Paper>

      <Button
        variant="contained"
        size="large"
        startIcon={<DownloadIcon />}
        href={DOWNLOAD_URL}
        target="_blank"
        rel="noopener noreferrer"
        component="a"
      >
        Download for Firefox
      </Button>

      <Typography variant="caption" display="block" sx={{ mt: 1, color: 'text.secondary' }}>
        Version 0.1.1 &mdash; Firefox only
      </Typography>
    </Box>
  )
}
