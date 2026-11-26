import React from 'react'
import { Box, Typography, Button, CircularProgress } from '@mui/material'
import GroupsIcon from '@mui/icons-material/Groups'
import AddIcon from '@mui/icons-material/Add'
import useRequireAuth from '../hooks/useRequireAuth'
import useLeagues from '../hooks/useLeagues'
import useCreateLeague from '../hooks/useCreateLeague'
import useLeagueInvitationResponse from '../hooks/useLeagueInvitationResponse'
import PendingInvitationsList from '../components/Leagues/PendingInvitationsList'
import LeagueList from '../components/Leagues/LeagueList'
import CreateLeagueDialog from '../components/Leagues/CreateLeagueDialog'

export default function Leagues() {
  const { user } = useRequireAuth()
  const { leagues, pendingInvitations, loading, refresh } = useLeagues()
  const createLeague = useCreateLeague()
  const invitationResponse = useLeagueInvitationResponse(refresh)

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}><CircularProgress /></Box>

  const hasPendingInvitations = pendingInvitations.length > 0

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <GroupsIcon /> Leagues
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={createLeague.open}>
          New League
        </Button>
      </Box>

      {hasPendingInvitations && <PendingInvitationsList invitations={pendingInvitations} response={invitationResponse} />}
      <LeagueList leagues={leagues} currentUserId={user?.id} />
      <CreateLeagueDialog createLeague={createLeague} />
    </Box>
  )
}
