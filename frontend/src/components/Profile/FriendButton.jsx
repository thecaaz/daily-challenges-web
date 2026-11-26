import React from 'react'
import { Button, CircularProgress, Box } from '@mui/material'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import CheckIcon from '@mui/icons-material/Check'
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty'
import { useAuth } from '../../contexts/AuthContext'
import useFriendStatus from '../../hooks/useFriendStatus'
import useFriendRequestActions from '../../hooks/useFriendRequestActions'
import useConfirm from '../../hooks/useConfirm'
import ConfirmDialog from '../ui/ConfirmDialog'

export default function FriendButton({ targetUserId }) {
  const { user } = useAuth()
  const { status, loading, error, refresh } = useFriendStatus(targetUserId)
  const { confirm, dialogProps } = useConfirm()
  const {
    isBusy, sendRequest, cancelRequest, acceptRequest, declineRequest, removeFriend,
  } = useFriendRequestActions(targetUserId, { onChanged: refresh, confirm })

  const isOwnProfileOrLoggedOut = !user || user.id === targetUserId
  if (isOwnProfileOrLoggedOut) return null

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
        startIcon={isBusy ? <CircularProgress size={14} /> : <PersonAddIcon />}
        disabled={isBusy}
        onClick={sendRequest}
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
          startIcon={isBusy ? <CircularProgress size={14} /> : <HourglassEmptyIcon />}
          disabled={isBusy}
          onClick={cancelRequest}
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
          startIcon={isBusy ? <CircularProgress size={14} /> : <CheckIcon />}
          disabled={isBusy}
          onClick={acceptRequest}
        >
          Accept
        </Button>
        <Button
          size="small"
          variant="outlined"
          color="error"
          disabled={isBusy}
          onClick={declineRequest}
        >
          Decline
        </Button>
      </Box>
    )
  }

  if (status === 'friends') {
    return (
      <>
        <Button
          size="small"
          variant="outlined"
          color="error"
          disabled={isBusy}
          startIcon={isBusy ? <CircularProgress size={14} /> : null}
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
