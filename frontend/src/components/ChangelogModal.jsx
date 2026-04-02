import React from 'react'
import { Dialog, DialogTitle, DialogContent, DialogActions, Typography, Box } from '@mui/material'
import AppButton from './ui/AppButton'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

export default function ChangelogModal({ open, onClose, version, rawVersion, changelog }) {
  return (
    <Dialog open={!!open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>{version ? `Changelog — ${version}` : 'Changelog'}</DialogTitle>
      <DialogContent dividers>
        {rawVersion && rawVersion !== version && (
          <Box sx={{ mb: 1 }}>
            <Typography variant="caption" color="text.secondary">Full: {rawVersion}</Typography>
          </Box>
        )}
        {changelog ? (
          <Box
            sx={(theme) => ({
              typography: 'body2',
              whiteSpace: 'normal',
              '& h1, & h2, & h3': { mt: 2, mb: 1 },
              '& p': { mb: 1 },
              '& a': { color: theme.palette.primary.main },
              '& ul, & ol': { pl: 3, mb: 1 },
              '& pre': {
                backgroundColor: theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.04)',
                padding: theme.spacing(1),
                borderRadius: theme.shape.borderRadius,
                overflow: 'auto',
                fontFamily: 'monospace',
                fontSize: '0.9rem',
                mb: 1,
              },
              '& code': { fontFamily: 'monospace', fontSize: '0.9rem' },
            })}
          >
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{changelog}</ReactMarkdown>
          </Box>
        ) : (
          <Typography color="text.secondary">No changelog available.</Typography>
        )}
      </DialogContent>
      <DialogActions>
        <AppButton variant="text" onClick={onClose} color="primary">Close</AppButton>
      </DialogActions>
    </Dialog>
  )
}
