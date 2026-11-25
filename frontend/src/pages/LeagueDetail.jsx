import React, { useState } from 'react'
import { Box, CircularProgress, Tabs, Tab, Button } from '@mui/material'
import ExitToAppIcon from '@mui/icons-material/ExitToApp'
import { useParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import useRequireAuth from '../hooks/useRequireAuth'
import useLeague from '../hooks/useLeague'
import useLeagueGameSummaries from '../hooks/useLeagueGameSummaries'
import useLeagueLeaderboard from '../hooks/useLeagueLeaderboard'
import useInviteByUsername from '../hooks/useInviteByUsername'
import useInviteLink from '../hooks/useInviteLink'
import useRenameLeague from '../hooks/useRenameLeague'
import useLeagueMembershipActions from '../hooks/useLeagueMembershipActions'
import LeagueHeader from '../components/League/LeagueHeader'
import LeagueOwnerActions from '../components/League/LeagueOwnerActions'
import LeagueGamesTab from '../components/League/LeagueGamesTab'
import LeagueMembersList from '../components/League/LeagueMembersList'
import LeagueLeaderboardTab from '../components/League/LeagueLeaderboardTab'
import LeagueInvitationsList from '../components/League/LeagueInvitationsList'
import InviteUserDialog from '../components/League/InviteUserDialog'
import InviteLinkDialog from '../components/League/InviteLinkDialog'
import RenameLeagueDialog from '../components/League/RenameLeagueDialog'

const TABS = { games: 0, members: 1, leaderboard: 2, invitations: 3 }

function TabPanel({ children, value, index }) {
  return value === index ? <Box sx={{ pt: 2 }}>{children}</Box> : null
}

export default function LeagueDetail() {
  useRequireAuth()
  const { id } = useParams()
  const { user } = useAuth()
  const [tab, setTab] = useState(TABS.games)

  const { league, loading, refresh: refreshLeague, setLeagueName } = useLeague(id)
  const gameSummaries = useLeagueGameSummaries(id)
  const leaderboardState = useLeagueLeaderboard(id, tab === TABS.leaderboard)
  const invite = useInviteByUsername(id, refreshLeague)
  const inviteLink = useInviteLink(id, refreshLeague)
  const rename = useRenameLeague(id, setLeagueName)
  const membership = useLeagueMembershipActions(id, {
    onLeagueChanged: refreshLeague,
    onMembersChanged: gameSummaries.refresh,
  })

  const showLeaderboardFor = (gameId, day = null) => {
    leaderboardState.selectGame(gameId, day)
    setTab(TABS.leaderboard)
  }

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}><CircularProgress /></Box>
  if (!league) return null

  const isOwner = league.ownerId === user?.id

  return (
    <Box>
      <LeagueHeader league={league}>
        {isOwner ? (
          <LeagueOwnerActions
            onInvite={invite.open}
            onCreateLink={inviteLink.create}
            isCreatingLink={inviteLink.isCreating}
            onRename={() => rename.open(league.name)}
            onDelete={membership.deleteLeague}
          />
        ) : (
          <Button size="small" startIcon={<ExitToAppIcon />} onClick={membership.leaveLeague} color="warning">
            Leave
          </Button>
        )}
      </LeagueHeader>

      <Tabs value={tab} onChange={(_, selectedTab) => setTab(selectedTab)} sx={{ mb: 1 }}>
        <Tab label="Games" />
        <Tab label="Members" />
        <Tab label="Leaderboard" />
        {isOwner && <Tab label="Invitations" />}
      </Tabs>

      <TabPanel value={tab} index={TABS.games}>
        <LeagueGamesTab summaries={gameSummaries} onSelectGame={showLeaderboardFor} />
      </TabPanel>

      <TabPanel value={tab} index={TABS.members}>
        <LeagueMembersList
          league={league}
          currentUserId={user?.id}
          canRemoveMembers={isOwner}
          onRemove={membership.removeMember}
        />
      </TabPanel>

      <TabPanel value={tab} index={TABS.leaderboard}>
        <LeagueLeaderboardTab leaderboardState={leaderboardState} currentUserId={user?.id} />
      </TabPanel>

      {isOwner && (
        <TabPanel value={tab} index={TABS.invitations}>
          <LeagueInvitationsList invitations={league.pendingInvitations} onCancel={membership.cancelInvitation} />
        </TabPanel>
      )}

      <InviteUserDialog leagueName={league.name} invite={invite} />
      <InviteLinkDialog inviteLink={inviteLink} />
      <RenameLeagueDialog rename={rename} />
    </Box>
  )
}
