import React, { useEffect, useState } from 'react'
import { Box, Button, Typography, Paper, List, ListItem, ListItemText, Chip } from '@mui/material'
import ExtensionIcon from '@mui/icons-material/Extension'
import DownloadIcon from '@mui/icons-material/Download'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import EXTENSION_UPDATES from '../../../extension/updates.json'
import { refreshAdapters, isExtensionDetected } from '../utils/adapters'

const DOWNLOAD_URL = '/extension.xpi'

// The released version is the one updates.json advertises (bumped only after
// the signed XPI is deployed), not manifest.json, which may already be ahead
// of the shipped /extension.xpi.
export function releasedVersion(updates) {
  const addons = updates && updates.addons ? Object.values(updates.addons) : []
  const list = addons[0] && Array.isArray(addons[0].updates) ? addons[0].updates : []
  const last = list[list.length - 1]
  return last && typeof last.version === 'string' ? last.version : null
}

const RELEASED_VERSION = releasedVersion(EXTENSION_UPDATES)

export default function Extension() {
  const [installed, setInstalled] = useState(null)

  useEffect(() => {
    let mounted = true
    // Always ask again here: the user may have just installed the extension.
    // A content script answering GET_ADAPTERS means the extension is present.
    refreshAdapters()
      .then(() => { if (mounted) setInstalled(isExtensionDetected() === true) })
      .catch(() => { if (mounted) setInstalled(false) })
    return () => { mounted = false }
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
          <ListItem disableGutters>
            <ListItemText primary="Cutle" />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText primary="BlindCut" />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText primary="MotionPath" />
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
        {RELEASED_VERSION ? <>Version {RELEASED_VERSION} &mdash; </> : null}Firefox only
      </Typography>
    </Box>
  )
}
