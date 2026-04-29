import React, { useState } from 'react'
import {
  Box, Typography, Button, CircularProgress, List, ListItem,
  ListItemText, ListItemSecondaryAction, Chip, Divider, Paper,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField
} from '@mui/material'
import GroupsIcon from '@mui/icons-material/Groups'
import AddIcon from '@mui/icons-material/Add'
import { Link, useNavigate } from 'react-router-dom'
import useRequireAuth from '../hooks/useRequireAuth'
import useLeagues from '../hooks/useLeagues'
import { useSnackbar } from '../contexts/SnackbarContext'
import api from '../api'

export default function Leagues() {
  useRequireAuth()
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const { leagues, pendingInvitations, loading, refresh } = useLeagues()

  const [createOpen, setCreateOpen] = useState(false)
  const [newName, setNewName] = useState('')
  const [createBusy, setCreateBusy] = useState(false)

  const handleCreate = async () => {
    if (!newName.trim()) return
    setCreateBusy(true)
    try {
      const res = await api.post('/leagues', { name: newName.trim() })
      showSnackbar(`League "${res.data.name}" created!`)
      setCreateOpen(false)
      setNewName('')
      navigate(`/leagues/${res.data.id}`)
    } catch (err) {
      const msg = err?.response?.data?.title || err?.response?.data || 'Failed to create league.'
      showSnackbar(typeof msg === 'string' ? msg : 'Something went wrong.', 'error')
    } finally {
      setCreateBusy(false)
    }
  }

  const handleAccept = async (inv) => {
    try {
      await api.post(`/leagues/invitations/${inv.id}/accept`)
      showSnackbar(`You joined "${inv.leagueName}"!`)
      refresh()
      navigate(`/leagues/${inv.leagueId}`)
    } catch {
      showSnackbar('Failed to accept invitation.', 'error')
    }
  }

  const handleDecline = async (inv) => {
    try {
      await api.post(`/leagues/invitations/${inv.id}/decline`)
      showSnackbar('Invitation declined.')
      refresh()
    } catch {
      showSnackbar('Failed to decline invitation.', 'error')
    }
  }

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}><CircularProgress /></Box>

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <GroupsIcon /> Leagues
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          New League
        </Button>
      </Box>

      {/* Pending invitations */}
      {pendingInvitations.length > 0 && (
        <Paper variant="outlined" sx={{ mb: 3, p: 2 }}>
          <Typography variant="subtitle1" gutterBottom fontWeight={600}>
            Pending Invitations
          </Typography>
          <List disablePadding>
            {pendingInvitations.map((inv, i) => (
              <React.Fragment key={inv.id}>
                {i > 0 && <Divider />}
                <ListItem disablePadding sx={{ py: 1 }}>
                  <ListItemText
                    primary={inv.leagueName}
                    secondary={`Invited by ${inv.inviterUsername}`}
                  />
                  <ListItemSecondaryAction>
                    <Button size="small" variant="contained" onClick={() => handleAccept(inv)} sx={{ mr: 1 }}>
                      Accept
                    </Button>
                    <Button size="small" onClick={() => handleDecline(inv)}>
                      Decline
                    </Button>
                  </ListItemSecondaryAction>
                </ListItem>
              </React.Fragment>
            ))}
          </List>
        </Paper>
      )}

      {/* My leagues */}
      {leagues.length === 0 ? (
        <Typography color="text.secondary">
          You are not in any leagues yet. Create one or ask a friend to invite you.
        </Typography>
      ) : (
        <List disablePadding>
          {leagues.map((league, i) => (
            <React.Fragment key={league.id}>
              {i > 0 && <Divider />}
              <ListItem
                component={Link}
                to={`/leagues/${league.id}`}
                sx={{ borderRadius: 1, '&:hover': { bgcolor: 'action.hover' }, textDecoration: 'none', color: 'inherit' }}
              >
                <ListItemText
                  primary={league.name}
                  secondary={`${league.memberCount} member${league.memberCount !== 1 ? 's' : ''} · Owner: ${league.ownerUsername}`}
                />
                {league.ownerId === league.ownerId && (
                  <Chip label={`${league.memberCount} members`} size="small" sx={{ ml: 1 }} />
                )}
              </ListItem>
            </React.Fragment>
          ))}
        </List>
      )}

      {/* Create dialog */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Create League</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="League name"
            fullWidth
            value={newName}
            onChange={e => setNewName(e.target.value)}
            inputProps={{ maxLength: 100 }}
            onKeyDown={e => { if (e.key === 'Enter') handleCreate() }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
          <Button onClick={handleCreate} disabled={createBusy || !newName.trim()} variant="contained">
            {createBusy ? <CircularProgress size={20} /> : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
