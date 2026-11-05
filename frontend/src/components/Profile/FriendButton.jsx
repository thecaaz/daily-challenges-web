import React, { useState } from 'react'
import { Button, CircularProgress, Box } from '@mui/material'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import CheckIcon from '@mui/icons-material/Check'
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty'
import { useAuth } from '../../contexts/AuthContext'
import { useSnackbar } from '../../contexts/SnackbarContext'
import useFriendStatus from '../../hooks/useFriendStatus'
import api from '../../api'
import { getApiErrorMessage } from '../../utils/apiError'
import useConfirm from '../../hooks/useConfirm'
import ConfirmDialog from '../ui/ConfirmDialog'

export default function FriendButton({ targetUserId }) {
  const { user } = useAuth()
  const { showSnackbar } = useSnackbar()
  const { status, loading, error, refresh } = useFriendStatus(targetUserId)
  const [busy, setBusy] = useState(false)
  const { confirm, dialogProps } = useConfirm()

  // Don't render if not logged in or viewing own profile
  if (!user || user.id === targetUserId) return null

  const handle = async (action) => {
    setBusy(true)
    try {
      await action()
      refresh()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Something went wrong.'), 'error')
    } finally {
      setBusy(false)
    }
  }

  if (loading) {
    return <CircularProgress size={20} />
  }

  if (error) {
    return (
      <Button size="small" variant="outlined" color="inherit" onClick={refresh}>
        Couldn't load friend status — Retry
      </Button>
    )
  }

  if (status === 'none') {
    return (
      <Button
        size="small"
        variant="outlined"
        startIcon={busy ? <CircularProgress size={14} /> : <PersonAddIcon />}
        disabled={busy}
        onClick={() => handle(() => api.post('/friends/requests', { targetUserId }))}
      >
        Add Friend
      </Button>
    )
  }

  if (status === 'pending_sent') {
    return (
      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button
          size="small"
          variant="outlined"
          color="inherit"
          startIcon={busy ? <CircularProgress size={14} /> : <HourglassEmptyIcon />}
          disabled={busy}
          onClick={() =>
            handle(async () => {
              // Need the request id — fetch sent requests to find it
              const res = await api.get('/friends/requests/sent')
              const req = res.data.find(r => r.receiverId === targetUserId)
              if (req) await api.delete(`/friends/requests/${req.id}`)
            })
          }
        >
          Request Sent — Cancel
        </Button>
      </Box>
    )
  }

  if (status === 'pending_received') {
    return (
      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button
          size="small"
          variant="contained"
          color="success"
          startIcon={busy ? <CircularProgress size={14} /> : <CheckIcon />}
          disabled={busy}
          onClick={() =>
            handle(async () => {
              const res = await api.get('/friends/requests/incoming')
              const req = res.data.find(r => r.senderId === targetUserId)
              if (req) await api.post(`/friends/requests/${req.id}/accept`)
            })
          }
        >
          Accept
        </Button>
        <Button
          size="small"
          variant="outlined"
          color="error"
          disabled={busy}
          onClick={() =>
            handle(async () => {
              const res = await api.get('/friends/requests/incoming')
              const req = res.data.find(r => r.senderId === targetUserId)
              if (req) await api.post(`/friends/requests/${req.id}/reject`)
            })
          }
        >
          Decline
        </Button>
      </Box>
    )
  }

  if (status === 'friends') {
    const removeFriend = async () => {
      const ok = await confirm({ title: 'Remove friend?', message: 'Remove this user from your friends?', confirmText: 'Remove' })
      if (ok) await handle(() => api.delete(`/friends/${targetUserId}`))
    }
    return (
      <>
        <Button
          size="small"
          variant="outlined"
          color="error"
          disabled={busy}
          startIcon={busy ? <CircularProgress size={14} /> : null}
          onClick={removeFriend}
        >
          Remove Friend
        </Button>
        <ConfirmDialog {...dialogProps} />
      </>
    )
  }

  return null
}
