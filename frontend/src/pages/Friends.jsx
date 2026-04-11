import React, { useEffect, useState, useCallback } from 'react'
import {
  Box, Typography, TextField, Button, CircularProgress,
  List, ListItem, ListItemText, ListItemSecondaryAction,
  Divider, Paper, Grid, Chip
} from '@mui/material'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import { Link } from 'react-router-dom'
import useRequireAuth from '../hooks/useRequireAuth'
import { useSnackbar } from '../contexts/SnackbarContext'
import api from '../api'

export default function Friends() {
  useRequireAuth()
  const { showSnackbar } = useSnackbar()

  const [username, setUsername] = useState('')
  const [sendBusy, setSendBusy] = useState(false)

  const [incoming, setIncoming] = useState([])
  const [sent, setSent] = useState([])
  const [friends, setFriends] = useState([])
  const [loading, setLoading] = useState(true)

  const fetchAll = useCallback(async () => {
    setLoading(true)
    try {
      const [inRes, sentRes, friendsRes] = await Promise.all([
        api.get('/friends/requests/incoming'),
        api.get('/friends/requests/sent'),
        api.get('/friends'),
      ])
      setIncoming(inRes.data)
      setSent(sentRes.data)
      setFriends(friendsRes.data)
    } catch {
      showSnackbar('Failed to load friends data.', 'error')
    } finally {
      setLoading(false)
    }
  }, [showSnackbar])

  useEffect(() => { fetchAll() }, [fetchAll])

  const sendByUsername = async () => {
    if (!username.trim()) return
    setSendBusy(true)
    try {
      await api.post('/friends/requests/by-username', { username: username.trim() })
      showSnackbar(`Friend request sent to ${username.trim()}.`)
      setUsername('')
      fetchAll()
    } catch (err) {
      const msg = err?.response?.data?.title || err?.response?.data || 'User not found or request already exists.'
      showSnackbar(typeof msg === 'string' ? msg : 'Something went wrong.', 'error')
    } finally {
      setSendBusy(false)
    }
  }

  const accept = async (req) => {
    try {
      await api.post(`/friends/requests/${req.id}/accept`)
      showSnackbar(`You are now friends with ${req.senderUsername}.`)
      fetchAll()
    } catch {
      showSnackbar('Failed to accept request.', 'error')
    }
  }

  const reject = async (req) => {
    try {
      await api.post(`/friends/requests/${req.id}/reject`)
      showSnackbar('Friend request declined.')
      fetchAll()
    } catch {
      showSnackbar('Failed to decline request.', 'error')
    }
  }

  const cancel = async (req) => {
    try {
      await api.delete(`/friends/requests/${req.id}`)
      showSnackbar('Friend request cancelled.')
      fetchAll()
    } catch {
      showSnackbar('Failed to cancel request.', 'error')
    }
  }

  const removeFriend = async (friend) => {
    try {
      await api.delete(`/friends/${friend.userId}`)
      showSnackbar(`Removed ${friend.username} from friends.`)
      fetchAll()
    } catch {
      showSnackbar('Failed to remove friend.', 'error')
    }
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Friends</Typography>

      {/* ── Add by username ───────────────────────────────────── */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Typography variant="h6" gutterBottom>Add Friend by Username</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            size="small"
            label="Username"
            value={username}
            onChange={e => setUsername(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && sendByUsername()}
            disabled={sendBusy}
            sx={{ flex: 1 }}
          />
          <Button
            variant="contained"
            startIcon={sendBusy ? <CircularProgress size={14} color="inherit" /> : <PersonAddIcon />}
            onClick={sendByUsername}
            disabled={sendBusy || !username.trim()}
          >
            Send Request
          </Button>
        </Box>
      </Paper>

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          {/* ── Incoming requests ─────────────────────────────── */}
          {incoming.length > 0 && (
            <Paper sx={{ mb: 3 }}>
              <Typography variant="h6" sx={{ p: 2, pb: 0 }}>
                Incoming Requests <Chip label={incoming.length} size="small" color="primary" sx={{ ml: 1 }} />
              </Typography>
              <List dense>
                {incoming.map((req, i) => (
                  <React.Fragment key={req.id}>
                    {i > 0 && <Divider component="li" />}
                    <ListItem>
                      <ListItemText
                        primary={
                          <Box component={Link} to={`/users/${req.senderId}`} sx={{ textDecoration: 'none', color: 'inherit', fontWeight: 500 }}>
                            {req.senderUsername}
                          </Box>
                        }
                      />
                      <ListItemSecondaryAction>
                        <Button size="small" variant="contained" color="success" onClick={() => accept(req)} sx={{ mr: 1 }}>
                          Accept
                        </Button>
                        <Button size="small" variant="outlined" color="error" onClick={() => reject(req)}>
                          Decline
                        </Button>
                      </ListItemSecondaryAction>
                    </ListItem>
                  </React.Fragment>
                ))}
              </List>
            </Paper>
          )}

          {/* ── Sent requests ─────────────────────────────────── */}
          {sent.length > 0 && (
            <Paper sx={{ mb: 3 }}>
              <Typography variant="h6" sx={{ p: 2, pb: 0 }}>Sent Requests</Typography>
              <List dense>
                {sent.map((req, i) => (
                  <React.Fragment key={req.id}>
                    {i > 0 && <Divider component="li" />}
                    <ListItem>
                      <ListItemText
                        primary={
                          <Box component={Link} to={`/users/${req.receiverId}`} sx={{ textDecoration: 'none', color: 'inherit', fontWeight: 500 }}>
                            {req.receiverUsername}
                          </Box>
                        }
                        secondary="Pending"
                      />
                      <ListItemSecondaryAction>
                        <Button size="small" variant="outlined" onClick={() => cancel(req)}>
                          Cancel
                        </Button>
                      </ListItemSecondaryAction>
                    </ListItem>
                  </React.Fragment>
                ))}
              </List>
            </Paper>
          )}

          {/* ── Friends list ──────────────────────────────────── */}
          <Paper>
            <Typography variant="h6" sx={{ p: 2, pb: 0 }}>
              Your Friends {friends.length > 0 && <Chip label={friends.length} size="small" sx={{ ml: 1 }} />}
            </Typography>
            {friends.length === 0 ? (
              <Typography color="text.secondary" sx={{ p: 2 }}>
                No friends yet. Send a request above!
              </Typography>
            ) : (
              <List dense>
                {friends.map((f, i) => (
                  <React.Fragment key={f.userId}>
                    {i > 0 && <Divider component="li" />}
                    <ListItem>
                      <ListItemText
                        primary={
                          <Box component={Link} to={`/users/${f.userId}`} sx={{ textDecoration: 'none', color: 'inherit', fontWeight: 500 }}>
                            {f.username}
                          </Box>
                        }
                        secondary={`Level ${f.level} · ${f.totalXp.toLocaleString()} XP · Streak: ${f.streak}`}
                      />
                      <ListItemSecondaryAction>
                        <Button size="small" variant="outlined" color="error" onClick={() => removeFriend(f)}>
                          Remove
                        </Button>
                      </ListItemSecondaryAction>
                    </ListItem>
                  </React.Fragment>
                ))}
              </List>
            )}
          </Paper>
        </>
      )}
    </Box>
  )
}
