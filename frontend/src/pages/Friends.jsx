import React from 'react'
import { Box, Typography, CircularProgress } from '@mui/material'
import useRequireAuth from '../hooks/useRequireAuth'
import useConfirm from '../hooks/useConfirm'
import useFriends from '../hooks/useFriends'
import useFriendActions from '../hooks/useFriendActions'
import useSendFriendRequest from '../hooks/useSendFriendRequest'
import ConfirmDialog from '../components/ui/ConfirmDialog'
import AddFriendForm from '../components/Friends/AddFriendForm'
import IncomingRequestsList from '../components/Friends/IncomingRequestsList'
import SentRequestsList from '../components/Friends/SentRequestsList'
import FriendsList from '../components/Friends/FriendsList'

export default function Friends() {
  useRequireAuth()
  const { confirm, dialogProps } = useConfirm()
  const { incoming, sent, friends, loading, refresh } = useFriends()
  const friendRequest = useSendFriendRequest(refresh)
  const actions = useFriendActions({ onChanged: refresh, confirm })

  const hasIncoming = incoming.length > 0
  const hasSent = sent.length > 0

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Friends</Typography>
      <AddFriendForm friendRequest={friendRequest} />

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          {hasIncoming && <IncomingRequestsList requests={incoming} actions={actions} />}
          {hasSent && <SentRequestsList requests={sent} actions={actions} />}
          <FriendsList friends={friends} actions={actions} />
        </>
      )}
      <ConfirmDialog {...dialogProps} />
    </Box>
  )
}
