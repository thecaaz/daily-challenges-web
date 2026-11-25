import React, { useState } from 'react'
import {
  Box, Typography, CircularProgress, Tabs, Tab, Button,
  List, ListItem, ListItemText, ListItemAvatar, Avatar,
  IconButton, Chip, Divider, TextField, Dialog,
  DialogTitle, DialogContent, DialogActions, Tooltip, Alert,
  Table, TableBody, TableCell, TableHead, TableRow, Select,
  MenuItem, FormControl, InputLabel, Grid, Skeleton,
  ToggleButtonGroup, ToggleButton
} from '@mui/material'
import GroupsIcon from '@mui/icons-material/Groups'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import LinkIcon from '@mui/icons-material/Link'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import ExitToAppIcon from '@mui/icons-material/ExitToApp'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import { useParams, Link } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import useRequireAuth from '../hooks/useRequireAuth'
import useLeagueGameSummaries from '../hooks/useLeagueGameSummaries'
import LeagueGameCard from '../components/League/LeagueGameCard'
import imageUrl from '../utils/imageUrl'
import useLeague from '../hooks/useLeague'
import useLeagueLeaderboard from '../hooks/useLeagueLeaderboard'
import useInviteByUsername from '../hooks/useInviteByUsername'
import useInviteLink from '../hooks/useInviteLink'
import useRenameLeague from '../hooks/useRenameLeague'
import useLeagueMembershipActions from '../hooks/useLeagueMembershipActions'

function TabPanel({ children, value, index }) {
  return value === index ? <Box sx={{ pt: 2 }}>{children}</Box> : null
}

export default function LeagueDetail() {
  useRequireAuth()
  const { id } = useParams()
  const { user } = useAuth()
  const { showSnackbar } = useSnackbar()
  const [tab, setTab] = useState(0)

  const { league, loading, refresh: refreshLeague, setLeagueName } = useLeague(id)
  const { items: gameSummaries, totalCount: gameSummaryTotal, loading: gsLoading, error: gsError, loadMore: loadMoreGames, refresh: refreshSummaries, days, setDays } = useLeagueGameSummaries(id)
  const leaderboardState = useLeagueLeaderboard(id, tab === 2)
  const invite = useInviteByUsername(id, refreshLeague)
  const inviteLink = useInviteLink(id, refreshLeague)
  const rename = useRenameLeague(id, setLeagueName)
  const { cancelInvitation: handleCancelInvitation, removeMember: handleKick, leaveLeague: handleLeave, deleteLeague: handleDelete } =
    useLeagueMembershipActions(id, { onLeagueChanged: refreshLeague, onMembersChanged: refreshSummaries })

  const isOwner = league?.ownerId === user?.id
  const { games, selectedGameId, displayedDay, leaderboard, loading: lbLoading } = leaderboardState

  const handleGameSelect = (gameId, day = null) => {
    leaderboardState.selectGame(gameId, day)
    setTab(2)
  }

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}><CircularProgress /></Box>
  if (!league) return null

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <GroupsIcon /> {league.name}
        </Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {isOwner && (
            <>
              <Button size="small" startIcon={<PersonAddIcon />} onClick={invite.open}>
                Invite
              </Button>
              <Button size="small" startIcon={<LinkIcon />} onClick={inviteLink.create} disabled={inviteLink.isCreating}>
                Create invite link
              </Button>
              <Button size="small" onClick={() => rename.open(league.name)}>
                Rename
              </Button>
              <Button size="small" color="error" startIcon={<DeleteOutlineIcon />} onClick={handleDelete}>
                Delete
              </Button>
            </>
          )}
          {!isOwner && (
            <Button size="small" startIcon={<ExitToAppIcon />} onClick={handleLeave} color="warning">
              Leave
            </Button>
          )}
        </Box>
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {league.memberCount ?? league.members?.length} members · Owner: {league.ownerUsername}
      </Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 1 }}>
        <Tab label="Games" />
        <Tab label="Members" />
        <Tab label="Leaderboard" />
        {isOwner && <Tab label="Invitations" />}
      </Tabs>

      {/* Games tab */}
      <TabPanel value={tab} index={0}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {gameSummaryTotal > 0 ? `${gameSummaryTotal} game${gameSummaryTotal !== 1 ? 's' : ''} played` : ''}
          </Typography>
          <ToggleButtonGroup
            size="small"
            value={days}
            exclusive
            onChange={(_, v) => { if (v != null) setDays(v) }}
          >
            <ToggleButton value={7}>7d</ToggleButton>
            <ToggleButton value={14}>14d</ToggleButton>
            <ToggleButton value={30}>30d</ToggleButton>
          </ToggleButtonGroup>
        </Box>
        {gsLoading && gameSummaries.length === 0 ? (
          <Grid container spacing={2}>
            {[0, 1, 2].map(i => (
              <Grid item xs={12} sm={6} md={4} key={i}>
                <Skeleton variant="rounded" height={200} />
              </Grid>
            ))}
          </Grid>
        ) : gameSummaries.length === 0 && gsError ? (
          <Alert
            severity="error"
            action={<Button color="inherit" size="small" onClick={refreshSummaries}>Retry</Button>}
          >
            Failed to load games.
          </Alert>
        ) : gameSummaries.length === 0 ? (
          <Alert severity="info">No games played in this league in the last {days} days.</Alert>
        ) : (
          <>
            <Grid container spacing={2}>
              {gameSummaries.map(g => (
                <Grid item xs={12} sm={6} md={4} key={g.gameId}>
                  <LeagueGameCard summary={g} onSelect={handleGameSelect} />
                </Grid>
              ))}
            </Grid>
            {gsError && (
          <Alert
            severity="error"
            sx={{ mt: 2 }}
            action={<Button color="inherit" size="small" onClick={loadMoreGames} disabled={gsLoading}>Retry</Button>}
          >
            Failed to load more games.
          </Alert>
        )}
        {gameSummaries.length < gameSummaryTotal && (
              <Box sx={{ textAlign: 'center', mt: 2 }}>
                <Button onClick={loadMoreGames} disabled={gsLoading}>
                  {gsLoading ? <CircularProgress size={20} /> : 'Load more'}
                </Button>
              </Box>
            )}
          </>
        )}
      </TabPanel>

      {/* Members tab */}
      <TabPanel value={tab} index={1}>
        <List disablePadding>
          {(league.members || []).map((m, i) => (
            <React.Fragment key={m.userId}>
              {i > 0 && <Divider />}
              <ListItem disablePadding sx={{ py: 0.5 }}>
                <ListItemAvatar>
                  <Avatar sx={{ width: 32, height: 32, fontSize: 14 }}>
                    {m.username?.[0]?.toUpperCase()}
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={
                    <Box component="span" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Link to={`/users/${m.userId}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                        {m.username}
                      </Link>
                      {(m.role === 'owner' || m.userId === league.ownerId) && <Chip label="Owner" size="small" color="primary" />}
                    </Box>
                  }
                  secondary={`Level ${m.level} · Streak ${m.streak}`}
                />
                {isOwner && m.userId !== user?.id && (
                  <Tooltip title="Remove member">
                    <IconButton size="small" onClick={() => handleKick(m.userId, m.username)}>
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
              </ListItem>
            </React.Fragment>
          ))}
        </List>
      </TabPanel>

      {/* Leaderboard tab */}
      <TabPanel value={tab} index={2}>
        <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel>Game</InputLabel>
            <Select
              value={selectedGameId}
              label="Game"
              onChange={e => leaderboardState.changeGame(e.target.value)}
            >
              {games.map(g => (
                <MenuItem key={g.id} value={g.id}>{g.name}</MenuItem>
              ))}
            </Select>
          </FormControl>
          <TextField
            type="date"
            size="small"
            label="Date"
            value={displayedDay}
            onChange={e => leaderboardState.changeDay(e.target.value)}
            InputLabelProps={{ shrink: true }}
          />
        </Box>

        {lbLoading && <CircularProgress size={24} sx={{ display: 'block', mx: 'auto', my: 2 }} />}
        {!lbLoading && !selectedGameId && (
          <Alert severity="info">Select a game above, or pick one from the Games tab.</Alert>
        )}

        {leaderboard && !lbLoading && leaderboard.scoresHidden && (
          <Alert severity="info" sx={{ mb: 2 }}>
            Scores for this day are hidden until you submit your own.
          </Alert>
        )}

        {leaderboard && !lbLoading && (
          (leaderboard.entries || []).length === 0 ? (
            <Alert severity="info">No submissions from league members for this game and date.</Alert>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>#</TableCell>
                  <TableCell>Player</TableCell>
                  <TableCell>Score</TableCell>
                  <TableCell>Screenshot</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {leaderboard.entries.map(entry => {
                  const hidden = leaderboard.scoresHidden || entry.score == null
                  return (
                  <TableRow key={entry.userId} sx={entry.userId === user?.id ? { bgcolor: 'action.selected' } : {}}>
                    <TableCell>
                      {hidden
                        ? '–'
                        : entry.rank === 1
                          ? <EmojiEventsIcon fontSize="small" sx={{ color: 'gold', verticalAlign: 'middle' }} />
                          : entry.rank}
                    </TableCell>
                    <TableCell>
                      <Link to={`/users/${entry.userId}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                        {entry.username}
                      </Link>
                    </TableCell>
                    <TableCell>
                      {hidden
                        ? <Typography variant="body2" color="text.secondary" component="span">Hidden</Typography>
                        : entry.score}
                    </TableCell>
                    <TableCell sx={{ py: 0.5 }}>
                      {!hidden && entry.screenshotUrl && entry.submissionId != null ? (
                        <Link to={`/submission/${entry.submissionId}`}>
                          <Box
                            component="img"
                            src={imageUrl(entry.screenshotUrl)}
                            alt={`${entry.username} screenshot`}
                            loading="lazy"
                            sx={{
                              height: 48,
                              width: 'auto',
                              maxWidth: 80,
                              borderRadius: 1,
                              objectFit: 'cover',
                              display: 'block',
                              cursor: 'pointer',
                              '&:hover': { opacity: 0.85 },
                            }}
                          />
                        </Link>
                      ) : (
                        <Typography variant="caption" color="text.disabled">—</Typography>
                      )}
                    </TableCell>
                  </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          )
        )}
      </TabPanel>

      {/* Invitations tab (owner only) */}
      {isOwner && (
        <TabPanel value={tab} index={3}>
          {(league.pendingInvitations || []).length === 0 ? (
            <Typography color="text.secondary">No pending invitations.</Typography>
          ) : (
            <List disablePadding>
              {(league.pendingInvitations || []).map((inv, i) => (
                <React.Fragment key={inv.id}>
                  {i > 0 && <Divider />}
                  <ListItem disablePadding sx={{ py: 0.5 }}>
                    <ListItemText
                      primary={inv.inviteeUsername ?? 'Link invite'}
                      secondary={`Sent by ${inv.inviterUsername}`}
                    />
                    <Tooltip title="Cancel invitation">
                      <IconButton size="small" onClick={() => handleCancelInvitation(inv.id)}>
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </ListItem>
                </React.Fragment>
              ))}
            </List>
          )}
        </TabPanel>
      )}

      {/* Invite by username dialog */}
      <Dialog open={invite.isOpen} onClose={invite.close} maxWidth="xs" fullWidth>
        <DialogTitle>Invite to {league.name}</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="Username"
            fullWidth
            value={invite.username}
            onChange={e => invite.setUsername(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter' && !invite.isSending) invite.send() }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={invite.close}>Cancel</Button>
          <Button onClick={invite.send} disabled={invite.isSending || !invite.username.trim()} variant="contained">
            {invite.isSending ? <CircularProgress size={20} /> : 'Send Invite'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Invite link dialog */}
      <Dialog open={inviteLink.isOpen} onClose={inviteLink.close} maxWidth="sm" fullWidth>
        <DialogTitle>Invite Link</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 1 }}>
            This is a single-use link for one person: the first person who opens it joins the league,
            then it stops working. It expires in 7 days. Create a new link for each person you want to invite.
          </Typography>
          <TextField
            fullWidth
            value={inviteLink.link}
            InputProps={{ readOnly: true }}
            onClick={e => e.target.select()}
          />
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              Promise.resolve()
                .then(() => navigator.clipboard.writeText(inviteLink.link))
                .then(() => showSnackbar('Link copied!'))
                .catch(() => showSnackbar('Could not copy the link. Select it and copy it manually.', 'error'))
            }}
          >
            Copy
          </Button>
          <Button onClick={inviteLink.close}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* Rename dialog */}
      <Dialog open={rename.isOpen} onClose={rename.close} maxWidth="xs" fullWidth>
        <DialogTitle>Rename League</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="New name"
            fullWidth
            value={rename.name}
            onChange={e => rename.setName(e.target.value)}
            inputProps={{ maxLength: 100 }}
            onKeyDown={e => { if (e.key === 'Enter' && !rename.isSaving) rename.save() }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={rename.close}>Cancel</Button>
          <Button onClick={rename.save} disabled={rename.isSaving || !rename.name.trim()} variant="contained">
            {rename.isSaving ? <CircularProgress size={20} /> : 'Rename'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
