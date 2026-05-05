import React, { useState, useEffect, useCallback } from 'react'
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
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import useRequireAuth from '../hooks/useRequireAuth'
import api from '../api'
import useLeagueGameSummaries from '../hooks/useLeagueGameSummaries'
import LeagueGameCard from '../components/League/LeagueGameCard'
import imageUrl from '../utils/imageUrl'

function TabPanel({ children, value, index }) {
  return value === index ? <Box sx={{ pt: 2 }}>{children}</Box> : null
}

export default function LeagueDetail() {
  useRequireAuth()
  const { id } = useParams()
  const { user } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const [league, setLeague] = useState(null)
  const [loading, setLoading] = useState(true)
  const [tab, setTab] = useState(0)

  // Invite dialog
  const [inviteOpen, setInviteOpen] = useState(false)
  const [inviteUsername, setInviteUsername] = useState('')
  const [inviteBusy, setInviteBusy] = useState(false)

  // Invite link dialog
  const [linkDialogOpen, setLinkDialogOpen] = useState(false)
  const [inviteLink, setInviteLink] = useState('')
  const [linkBusy, setLinkBusy] = useState(false)

  // Rename dialog
  const [renameOpen, setRenameOpen] = useState(false)
  const [newName, setNewName] = useState('')
  const [renameBusy, setRenameBusy] = useState(false)

  // Leaderboard
  const [games, setGames] = useState([])
  const [selectedGameId, setSelectedGameId] = useState('')
  const [selectedDay, setSelectedDay] = useState(new Date().toISOString().slice(0, 10))
  const [leaderboard, setLeaderboard] = useState(null)
  const [lbLoading, setLbLoading] = useState(false)

  const fetchLeague = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get(`/leagues/${id}`)
      setLeague(res.data)
    } catch (err) {
      if (err?.response?.status === 404) showSnackbar('League not found.', 'error')
      else showSnackbar('Failed to load league.', 'error')
      navigate('/leagues')
    } finally {
      setLoading(false)
    }
  }, [id, navigate, showSnackbar])

  useEffect(() => { fetchLeague() }, [fetchLeague])

  useEffect(() => {
    api.get('/games').then(r => setGames(r.data || [])).catch(() => {})
  }, [])

  const isOwner = league?.ownerId === user?.id

  const handleInvite = async () => {
    if (!inviteUsername.trim()) return
    setInviteBusy(true)
    try {
      await api.post(`/leagues/${id}/invitations`, { username: inviteUsername.trim() })
      showSnackbar(`Invitation sent to ${inviteUsername.trim()}.`)
      setInviteOpen(false)
      setInviteUsername('')
    } catch (err) {
      const msg = err?.response?.data?.title || err?.response?.data || 'Could not send invitation.'
      showSnackbar(typeof msg === 'string' ? msg : 'Something went wrong.', 'error')
    } finally {
      setInviteBusy(false)
    }
  }

  const handleCreateLink = async () => {
    setLinkBusy(true)
    try {
      const res = await api.post(`/leagues/${id}/invite-link`)
      const token = res.data.token
      const url = `${window.location.origin}/leagues/join/${token}`
      setInviteLink(url)
      setLinkDialogOpen(true)
    } catch {
      showSnackbar('Failed to create invite link.', 'error')
    } finally {
      setLinkBusy(false)
    }
  }

  const handleCancelInvitation = async (invId) => {
    try {
      await api.delete(`/leagues/invitations/${invId}`)
      showSnackbar('Invitation cancelled.')
      fetchLeague()
    } catch {
      showSnackbar('Failed to cancel invitation.', 'error')
    }
  }

  const handleKick = async (memberId, username) => {
    if (!window.confirm(`Remove ${username} from this league?`)) return
    try {
      await api.delete(`/leagues/${id}/members/${memberId}`)
      showSnackbar(`${username} removed.`)
      fetchLeague()
    } catch {
      showSnackbar('Failed to remove member.', 'error')
    }
  }

  const handleLeave = async () => {
    if (!window.confirm('Leave this league?')) return
    try {
      await api.post(`/leagues/${id}/leave`)
      showSnackbar('You left the league.')
      navigate('/leagues')
    } catch (err) {
      const msg = err?.response?.data?.title || err?.response?.data || 'Failed to leave.'
      showSnackbar(typeof msg === 'string' ? msg : 'Something went wrong.', 'error')
    }
  }

  const handleDelete = async () => {
    if (!window.confirm('Delete this league? This cannot be undone.')) return
    try {
      await api.delete(`/leagues/${id}`)
      showSnackbar('League deleted.')
      navigate('/leagues')
    } catch {
      showSnackbar('Failed to delete league.', 'error')
    }
  }

  const handleRename = async () => {
    if (!newName.trim()) return
    setRenameBusy(true)
    try {
      const res = await api.patch(`/leagues/${id}/name`, { name: newName.trim() })
      setLeague(prev => ({ ...prev, name: res.data.name }))
      showSnackbar('League renamed.')
      setRenameOpen(false)
    } catch {
      showSnackbar('Failed to rename league.', 'error')
    } finally {
      setRenameBusy(false)
    }
  }

  const { items: gameSummaries, totalCount: gameSummaryTotal, loading: gsLoading, loadMore: loadMoreGames, days, setDays } = useLeagueGameSummaries(id)

  const fetchLeaderboard = useCallback(async () => {
    if (!selectedGameId) return
    setLbLoading(true)
    try {
      const res = await api.get(`/leagues/${id}/leaderboard`, {
        params: { gameId: selectedGameId, scoringDay: selectedDay }
      })
      setLeaderboard(res.data)
    } catch {
      showSnackbar('Failed to load leaderboard.', 'error')
    } finally {
      setLbLoading(false)
    }
  }, [id, selectedGameId, selectedDay, showSnackbar])

  // Auto-load leaderboard when on that tab or when game/date selection changes
  useEffect(() => { if (tab === 2 && selectedGameId) fetchLeaderboard() }, [tab, fetchLeaderboard])

  const handleGameSelect = (gameId, day = null) => {
    setSelectedGameId(gameId)
    setSelectedDay(day ?? new Date().toISOString().slice(0, 10))
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
              <Button size="small" startIcon={<PersonAddIcon />} onClick={() => setInviteOpen(true)}>
                Invite
              </Button>
              <Button size="small" startIcon={<LinkIcon />} onClick={handleCreateLink} disabled={linkBusy}>
                Invite Link
              </Button>
              <Button size="small" onClick={() => { setNewName(league.name); setRenameOpen(true) }}>
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
                      {m.role === 'owner' && <Chip label="Owner" size="small" color="primary" />}
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
              onChange={e => setSelectedGameId(e.target.value)}
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
            value={selectedDay}
            onChange={e => setSelectedDay(e.target.value)}
            InputLabelProps={{ shrink: true }}
          />
        </Box>

        {lbLoading && <CircularProgress size={24} sx={{ display: 'block', mx: 'auto', my: 2 }} />}
        {!lbLoading && !selectedGameId && (
          <Alert severity="info">Select a game above, or pick one from the Games tab.</Alert>
        )}

        {leaderboard && !lbLoading && (
          leaderboard.entries.length === 0 ? (
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
                {leaderboard.entries.map(entry => (
                  <TableRow key={entry.userId} sx={entry.userId === user?.id ? { bgcolor: 'action.selected' } : {}}>
                    <TableCell>
                      {entry.rank === 1
                        ? <EmojiEventsIcon fontSize="small" sx={{ color: 'gold', verticalAlign: 'middle' }} />
                        : entry.rank}
                    </TableCell>
                    <TableCell>
                      <Link to={`/users/${entry.userId}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                        {entry.username}
                      </Link>
                    </TableCell>
                    <TableCell>{entry.score}</TableCell>
                    <TableCell sx={{ py: 0.5 }}>
                      {entry.screenshotUrl ? (
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
                ))}
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
      <Dialog open={inviteOpen} onClose={() => setInviteOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Invite to {league.name}</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="Username"
            fullWidth
            value={inviteUsername}
            onChange={e => setInviteUsername(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter') handleInvite() }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInviteOpen(false)}>Cancel</Button>
          <Button onClick={handleInvite} disabled={inviteBusy || !inviteUsername.trim()} variant="contained">
            {inviteBusy ? <CircularProgress size={20} /> : 'Send Invite'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Invite link dialog */}
      <Dialog open={linkDialogOpen} onClose={() => setLinkDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Invite Link</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 1 }}>Share this link to invite anyone (expires in 7 days):</Typography>
          <TextField
            fullWidth
            value={inviteLink}
            InputProps={{ readOnly: true }}
            onClick={e => e.target.select()}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { navigator.clipboard.writeText(inviteLink); showSnackbar('Link copied!') }}>
            Copy
          </Button>
          <Button onClick={() => setLinkDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* Rename dialog */}
      <Dialog open={renameOpen} onClose={() => setRenameOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Rename League</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="New name"
            fullWidth
            value={newName}
            onChange={e => setNewName(e.target.value)}
            inputProps={{ maxLength: 100 }}
            onKeyDown={e => { if (e.key === 'Enter') handleRename() }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRenameOpen(false)}>Cancel</Button>
          <Button onClick={handleRename} disabled={renameBusy || !newName.trim()} variant="contained">
            {renameBusy ? <CircularProgress size={20} /> : 'Rename'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
