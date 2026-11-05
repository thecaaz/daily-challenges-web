import React, { useEffect, useState, useCallback, useRef } from 'react'
import {
  Box, Typography, TextField, Button, CircularProgress,
  List, ListItem, ListItemText, ListItemSecondaryAction,
  Divider, Paper, Chip
} from '@mui/material'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import { Link } from 'react-router-dom'
import useRequireAuth from '../hooks/useRequireAuth'
import { useSnackbar } from '../contexts/SnackbarContext'
import api from '../api'
import { getApiErrorMessage } from '../utils/apiError'
import useConfirm from '../hooks/useConfirm'
import formatNumber from '../utils/formatNumber'
import ConfirmDialog from '../components/ui/ConfirmDialog'

export default function Friends() {
  useRequireAuth()
  const { showSnackbar } = useSnackbar()
  const { confirm, dialogProps } = useConfirm()
  // Keys of the rows with a request in flight ('req:<id>' or 'friend:<userId>').
  const [busyKeys, setBusyKeys] = useState(() => new Set())
  const busyRef = useRef(new Set()) // guards double clicks before the re-render
  const isBusy = (key) => busyKeys.has(key)

  const [username, setUsername] = useState('')
  const [sendBusy, setSendBusy] = useState(false)

  const [incoming, setIncoming] = useState([])
  const [sent, setSent] = useState([])
  const [friends, setFriends] = useState([])
  const [loading, setLoading] = useState(true)

  // `silent` refreshes after an action without replacing the lists by a spinner.
  const fetchAll = useCallback(async ({ silent = false } = {}) => {
    if (!silent) setLoading(true)
    try {
      const [inRes, sentRes, friendsRes] = await Promise.all([
        api.get('/friends/requests/incoming'),
        api.get('/friends/requests/sent'),
        api.get('/friends'),
      ])
      setIncoming(inRes.data ?? [])
      setSent(sentRes.data ?? [])
      setFriends(friendsRes.data ?? [])
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
      fetchAll({ silent: true })
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'User not found or request already exists.'), 'error')
    } finally {
      setSendBusy(false)
    }
  }

  // Runs one row action with that row's buttons disabled while it's in flight.
  const runAction = async (key, action, successMessage, errorFallback) => {
    if (busyRef.current.has(key)) return
    busyRef.current.add(key)
    setBusyKeys(prev => new Set(prev).add(key))
    try {
      await action()
      showSnackbar(successMessage)
      await fetchAll({ silent: true })
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, errorFallback), 'error')
    } finally {
      busyRef.current.delete(key)
      setBusyKeys(prev => {
        const next = new Set(prev)
        next.delete(key)
        return next
      })
    }
  }

  const accept = (req) => runAction(
    `req:${req.id}`,
    () => api.post(`/friends/requests/${req.id}/accept`),
    `You are now friends with ${req.senderUsername}.`,
    'Failed to accept request.',
  )

  const reject = (req) => runAction(
    `req:${req.id}`,
    () => api.post(`/friends/requests/${req.id}/reject`),
    'Friend request declined.',
    'Failed to decline request.',
  )

  const cancel = (req) => runAction(
    `req:${req.id}`,
    () => api.delete(`/friends/requests/${req.id}`),
    'Friend request cancelled.',
    'Failed to cancel request.',
  )

  const removeFriend = async (friend) => {
    const ok = await confirm({
      title: 'Remove friend?',
      message: `Remove ${friend.username} from your friends?`,
      confirmText: 'Remove',
    })
    if (!ok) return
    await runAction(
      `friend:${friend.userId}`,
      () => api.delete(`/friends/${friend.userId}`),
      `Removed ${friend.username} from friends.`,
      'Failed to remove friend.',
    )
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
            onKeyDown={e => { if (e.key === 'Enter' && !sendBusy) sendByUsername() }}
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
                        <Button size="small" variant="contained" color="success" onClick={() => accept(req)} disabled={isBusy(`req:${req.id}`)} sx={{ mr: 1 }}>
                          Accept
                        </Button>
                        <Button size="small" variant="outlined" color="error" onClick={() => reject(req)} disabled={isBusy(`req:${req.id}`)}>
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
                        <Button size="small" variant="outlined" onClick={() => cancel(req)} disabled={isBusy(`req:${req.id}`)}>
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
                        secondary={`Level ${f.level ?? 1} · ${formatNumber(f.totalXp ?? 0)} XP · Streak: ${f.streak ?? 0}`}
                      />
                      <ListItemSecondaryAction>
                        <Button size="small" variant="outlined" color="error" onClick={() => removeFriend(f)} disabled={isBusy(`friend:${f.userId}`)}>
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
      <ConfirmDialog {...dialogProps} />
    </Box>
  )
}
