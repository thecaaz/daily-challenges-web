import React from 'react'
import { Box, Button, Typography, Paper, List, ListItem, ListItemText, Chip } from '@mui/material'
import ExtensionIcon from '@mui/icons-material/Extension'
import DownloadIcon from '@mui/icons-material/Download'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import EXTENSION_UPDATES from '../../../extension/updates.json'
import useExtensionInstalled from '../hooks/useExtensionInstalled'

const DOWNLOAD_URL = '/extension.xpi'

const FEATURES = [
  'Reads your score automatically from supported game pages',
  'Captures a screenshot of your result for submission',
  'Pre-fills the submission form so you only need to confirm',
]

const SUPPORTED_GAMES = ['TimeGuessr', 'MapTap', 'Cutle', 'BlindCut', 'MotionPath']

/**
 * The released version is the one updates.json advertises (bumped only after
 * the signed XPI is deployed), not manifest.json, which may already be ahead
 * of the shipped /extension.xpi.
 */
export function releasedVersion(updates) {
  const addons = updates && updates.addons ? Object.values(updates.addons) : []
  const list = addons[0] && Array.isArray(addons[0].updates) ? addons[0].updates : []
  const last = list[list.length - 1]
  return last && typeof last.version === 'string' ? last.version : null
}

const RELEASED_VERSION = releasedVersion(EXTENSION_UPDATES)

function PlainList({ items }) {
  return (
    <List dense disablePadding>
      {items.map(item => (
        <ListItem disableGutters key={item}>
          <ListItemText primary={item} />
        </ListItem>
      ))}
    </List>
  )
}

export default function Extension() {
  const installed = useExtensionInstalled()

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
        <PlainList items={FEATURES} />
        <Typography variant="subtitle2" sx={{ mt: 1 }}>Supported games</Typography>
        <PlainList items={SUPPORTED_GAMES} />
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
        {RELEASED_VERSION && <>Version {RELEASED_VERSION} &mdash; </>}Firefox only
      </Typography>
    </Box>
  )
}
