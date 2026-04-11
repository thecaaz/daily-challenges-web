import React, { useEffect, useState, useCallback } from 'react'
import {
  Box, Typography, Grid, Paper, Avatar, Chip, Divider,
  List, ListItem, ListItemAvatar, ListItemText, Skeleton,
  Tooltip, IconButton, Stack
} from '@mui/material'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import ExtensionIcon from '@mui/icons-material/Extension'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import PeopleIcon from '@mui/icons-material/People'
import TodayIcon from '@mui/icons-material/Today'
import SportsSoccerIcon from '@mui/icons-material/SportsSoccer'
import WhatshotIcon from '@mui/icons-material/Whatshot'
import { Link } from 'react-router-dom'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import useFavorite from '../hooks/useFavorite'
import PlayButton from '../components/ui/PlayButton'
import { hasAdapterForUrl } from '../utils/adapters'
import timeAgo from '../utils/timeAgo'

// ─── Game list row ────────────────────────────────────────────────────────────

function GameRow({ game, badge, badgeLabel }) {
  const { isFavorite, toggle: toggleFavorite, loading: favLoading } = useFavorite(game.id, game.isFavorite)
  const [hasAdapter, setHasAdapter] = useState(null)

  useEffect(() => {
    let mounted = true
    if (!game?.url) { setHasAdapter(false); return }
    hasAdapterForUrl(game.url).then(ok => { if (mounted) setHasAdapter(Boolean(ok)) })
    return () => { mounted = false }
  }, [game?.url])

  return (
    <ListItem
      disableGutters
      sx={{
        py: 1,
        px: 1.5,
        borderRadius: 2,
        transition: 'background 0.15s',
        '&:hover': { bgcolor: 'action.hover' },
        gap: 1.5,
        alignItems: 'center',
      }}
    >
      {/* Game image */}
      <ListItemAvatar sx={{ minWidth: 44 }}>
        <Avatar
          src={game.imageUrl ?? undefined}
          variant="rounded"
          sx={{ width: 40, height: 40, bgcolor: 'primary.dark', fontSize: '1.2rem' }}
        >
          {!game.imageUrl && '🎮'}
        </Avatar>
      </ListItemAvatar>

      {/* Name + icons */}
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap' }}>
          <Link to={`/games/${game.id}`} style={{ textDecoration: 'none', color: 'inherit' }}>
            <Typography variant="body2" fontWeight={600} noWrap sx={{ maxWidth: 160 }}>
              {game.name}
            </Typography>
          </Link>
          {hasAdapter && (
            <Tooltip title="Playable in browser via extension">
              <ExtensionIcon sx={{ fontSize: 15, color: 'primary.main', flexShrink: 0 }} />
            </Tooltip>
          )}
          {game.hasSubmittedForLatest && (
            <Tooltip title="Already submitted today">
              <CheckCircleIcon sx={{ fontSize: 15, color: 'success.main', flexShrink: 0 }} />
            </Tooltip>
          )}
        </Box>
        {badge !== undefined && (
          <Chip
            label={badgeLabel}
            size="small"
            color="primary"
            variant="outlined"
            sx={{ height: 18, fontSize: '0.65rem', mt: 0.25 }}
          />
        )}
      </Box>

      {/* Favourite */}
      <Tooltip title={isFavorite ? 'Remove from favourites' : 'Add to favourites'}>
        <IconButton size="small" onClick={toggleFavorite} disabled={favLoading} sx={{ p: 0.5 }}>
          {isFavorite
            ? <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
            : <StarBorderIcon fontSize="small" sx={{ color: 'text.secondary' }} />}
        </IconButton>
      </Tooltip>

      {/* Play */}
      <PlayButton game={game} size="small" adapter={hasAdapter} sx={{ flexShrink: 0 }} />
    </ListItem>
  )
}

// ─── Friend activity row ──────────────────────────────────────────────────────

function FriendActivityRow({ activity }) {
  const { isFavorite, toggle: toggleFavorite, loading: favLoading } = useFavorite(activity.gameId, activity.isFavorite)
  const [hasAdapter, setHasAdapter] = useState(null)

  const fakeGame = { id: activity.gameId, url: activity.gameUrl, name: activity.gameName, imageUrl: activity.gameImageUrl, isFavorite: activity.isFavorite, hasSubmittedForLatest: activity.hasSubmittedForLatest }

  useEffect(() => {
    let mounted = true
    if (!activity.gameUrl) { setHasAdapter(false); return }
    hasAdapterForUrl(activity.gameUrl).then(ok => { if (mounted) setHasAdapter(Boolean(ok)) })
    return () => { mounted = false }
  }, [activity.gameUrl])

  const latest = activity.recentSubmissions?.[0]

  return (
    <ListItem
      disableGutters
      sx={{
        py: 1,
        px: 1.5,
        borderRadius: 2,
        transition: 'background 0.15s',
        '&:hover': { bgcolor: 'action.hover' },
        gap: 1.5,
        alignItems: 'center',
      }}
    >
      <ListItemAvatar sx={{ minWidth: 44 }}>
        <Avatar
          src={activity.gameImageUrl ?? undefined}
          variant="rounded"
          sx={{ width: 40, height: 40, bgcolor: 'secondary.dark', fontSize: '1.2rem' }}
        >
          {!activity.gameImageUrl && '🎮'}
        </Avatar>
      </ListItemAvatar>

      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <Link to={`/games/${activity.gameId}`} style={{ textDecoration: 'none', color: 'inherit' }}>
            <Typography variant="body2" fontWeight={600} noWrap sx={{ maxWidth: 140 }}>
              {activity.gameName}
            </Typography>
          </Link>
          {hasAdapter && (
            <Tooltip title="Playable in browser via extension">
              <ExtensionIcon sx={{ fontSize: 15, color: 'primary.main', flexShrink: 0 }} />
            </Tooltip>
          )}
          {activity.hasSubmittedForLatest && (
            <Tooltip title="Already submitted today">
              <CheckCircleIcon sx={{ fontSize: 15, color: 'success.main', flexShrink: 0 }} />
            </Tooltip>
          )}
        </Box>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.4, mt: 0.25 }}>
          {activity.recentSubmissions?.slice(0, 4).map((s, i) => (
            <Chip
              key={i}
              label={s.username}
              size="small"
              sx={{ height: 18, fontSize: '0.65rem' }}
            />
          ))}
          {latest && (
            <Typography variant="caption" color="text.secondary" sx={{ alignSelf: 'center', ml: 0.5 }}>
              {timeAgo(latest.submittedAt)}
            </Typography>
          )}
        </Box>
      </Box>

      <Tooltip title={isFavorite ? 'Remove from favourites' : 'Add to favourites'}>
        <IconButton size="small" onClick={toggleFavorite} disabled={favLoading} sx={{ p: 0.5 }}>
          {isFavorite
            ? <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
            : <StarBorderIcon fontSize="small" sx={{ color: 'text.secondary' }} />}
        </IconButton>
      </Tooltip>

      <PlayButton game={fakeGame} size="small" adapter={hasAdapter} sx={{ flexShrink: 0 }} />
    </ListItem>
  )
}

// ─── Stat chip ────────────────────────────────────────────────────────────────

function StatCard({ icon, label, value, color = 'primary' }) {
  return (
    <Paper
      elevation={0}
      sx={{
        px: 2.5, py: 1.5,
        borderRadius: 3,
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        border: '1px solid',
        borderColor: 'divider',
        bgcolor: 'background.paper',
        minWidth: 130,
      }}
    >
      <Avatar sx={{ width: 36, height: 36, bgcolor: `${color}.main` }}>{icon}</Avatar>
      <Box>
        <Typography variant="h6" fontWeight={700} lineHeight={1}>{value}</Typography>
        <Typography variant="caption" color="text.secondary">{label}</Typography>
      </Box>
    </Paper>
  )
}

// ─── Section panel ────────────────────────────────────────────────────────────

function Section({ title, icon, children, empty, emptyText = 'Nothing here yet.' }) {
  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 3,
        border: '1px solid',
        borderColor: 'divider',
        overflow: 'hidden',
        height: '100%',
      }}
    >
      <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1, bgcolor: 'background.default' }}>
        {icon}
        <Typography variant="subtitle1" fontWeight={700}>{title}</Typography>
      </Box>
      <Divider />
      {empty
        ? <Box sx={{ px: 2, py: 3, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">{emptyText}</Typography>
          </Box>
        : <List dense disablePadding sx={{ px: 0.5, py: 0.5 }}>{children}</List>
      }
    </Paper>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function DashboardTest() {
  useRequireAuth()

  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)

  const fetchData = useCallback(async () => {
    try {
      const res = await api.get('/dashboard')
      setData(res.data)
    } catch {
      setData({ recentGames: [], friendActivity: [], friends: [] })
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { fetchData() }, [fetchData])

  // Keep dashboard in sync when favorites are toggled elsewhere
  useEffect(() => {
    const handler = (ev) => {
      const { gameId, isFavorite } = ev?.detail || {}
      if (typeof gameId === 'undefined') return
      setData(prev => {
        if (!prev) return prev
        return {
          ...prev,
          recentGames: prev.recentGames.map(a =>
            String(a.game.id) === String(gameId) ? { ...a, game: { ...a.game, isFavorite } } : a
          ),
          friendActivity: prev.friendActivity.map(a =>
            String(a.gameId) === String(gameId) ? { ...a, isFavorite } : a
          ),
        }
      })
    }
    window.addEventListener('favorite-changed', handler)
    return () => window.removeEventListener('favorite-changed', handler)
  }, [])

  if (loading) {
    return (
      <Box>
        <Skeleton variant="text" width={200} height={36} sx={{ mb: 3 }} />
        <Stack direction="row" spacing={2} sx={{ mb: 3, flexWrap: 'wrap', gap: 2 }}>
          {[1, 2, 3].map(i => <Skeleton key={i} variant="rounded" width={150} height={64} />)}
        </Stack>
        <Grid container spacing={2}>
          {[1, 2].map(i => (
            <Grid item xs={12} md={6} key={i}>
              <Skeleton variant="rounded" height={300} />
            </Grid>
          ))}
          <Grid item xs={12}>
            <Skeleton variant="rounded" height={200} />
          </Grid>
        </Grid>
      </Box>
    )
  }

  const { recentGames = [], friendActivity = [], friends = [] } = data || {}
  const totalActiveToday = recentGames.reduce((sum, a) => sum + (a.todayCount || 0), 0)

  return (
    <Box>
      {/* Page title */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4" fontWeight={800} gutterBottom>Dashboard</Typography>
        <Typography variant="body2" color="text.secondary">Your daily overview</Typography>
      </Box>

      {/* Stat summary strip */}
      <Stack direction="row" spacing={2} sx={{ mb: 3, flexWrap: 'wrap', gap: 2 }}>
        <StatCard
          icon={<TodayIcon fontSize="small" />}
          label="Active today"
          value={recentGames.length}
          color="primary"
        />
        <StatCard
          icon={<SportsSoccerIcon fontSize="small" />}
          label="Submissions today"
          value={totalActiveToday}
          color="success"
        />
        <StatCard
          icon={<PeopleIcon fontSize="small" />}
          label="Friends"
          value={friends.length}
          color="secondary"
        />
        {friendActivity.length > 0 && (
          <StatCard
            icon={<WhatshotIcon fontSize="small" />}
            label="Friend activity"
            value={`${friendActivity.length} game${friendActivity.length !== 1 ? 's' : ''}`}
            color="warning"
          />
        )}
      </Stack>

      {/* Main grid: two columns on md+, stacked on mobile */}
      <Grid container spacing={2} sx={{ mb: 2 }}>

        {/* Recent Games */}
        <Grid item xs={12} md={6}>
          <Section
            title="Active Today"
            icon={<TodayIcon fontSize="small" color="primary" />}
            empty={recentGames.length === 0}
            emptyText="No games have submissions today yet."
          >
            {recentGames.map(activity => (
              <GameRow
                key={activity.game.id}
                game={activity.game}
                badge={activity.todayCount}
                badgeLabel={`${activity.todayCount} player${activity.todayCount !== 1 ? 's' : ''} today`}
              />
            ))}
          </Section>
        </Grid>

        {/* Friends' Activity */}
        <Grid item xs={12} md={6}>
          <Section
            title="Friends' Activity"
            icon={<WhatshotIcon fontSize="small" color="warning" />}
            empty={friendActivity.length === 0}
            emptyText={friends.length === 0 ? "Add friends to see what they're playing." : "No friend activity in the last 7 days."}
          >
            {friendActivity.map(activity => (
              <FriendActivityRow key={activity.gameId} activity={activity} />
            ))}
          </Section>
        </Grid>

      </Grid>

      {/* Friends list — full width */}
      <Paper
        elevation={0}
        sx={{
          borderRadius: 3,
          border: '1px solid',
          borderColor: 'divider',
          overflow: 'hidden',
        }}
      >
        <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1, bgcolor: 'background.default' }}>
          <PeopleIcon fontSize="small" color="secondary" />
          <Typography variant="subtitle1" fontWeight={700}>Friends</Typography>
          <Chip label={friends.length} size="small" sx={{ ml: 0.5 }} />
        </Box>
        <Divider />
        {friends.length === 0
          ? (
            <Box sx={{ px: 2, py: 3, textAlign: 'center' }}>
              <Typography variant="body2" color="text.secondary">
                No friends yet.{' '}
                <Link to="/friends" style={{ color: 'inherit' }}>Add some!</Link>
              </Typography>
            </Box>
          )
          : (
            <Grid container spacing={0}>
              {friends.map((friend, i) => (
                <Grid item xs={12} sm={6} md={4} key={friend.userId}>
                  {i > 0 && i % 1 === 0 && <Divider />}
                  <ListItem
                    component={Link}
                    to={`/users/${friend.userId}`}
                    disableGutters
                    sx={{
                      px: 2,
                      py: 1.25,
                      gap: 1.5,
                      textDecoration: 'none',
                      color: 'inherit',
                      transition: 'background 0.15s',
                      '&:hover': { bgcolor: 'action.hover' },
                    }}
                  >
                    <ListItemAvatar sx={{ minWidth: 44 }}>
                      <Avatar sx={{ width: 38, height: 38, bgcolor: 'secondary.main', fontWeight: 700, fontSize: '0.9rem' }}>
                        {friend.username.slice(0, 2).toUpperCase()}
                      </Avatar>
                    </ListItemAvatar>
                    <ListItemText
                      primary={
                        <Typography variant="body2" fontWeight={600}>{friend.username}</Typography>
                      }
                      secondary={
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap', mt: 0.25 }}>
                          <Chip label={`Lv ${friend.level}`} size="small" color="primary" sx={{ height: 18, fontSize: '0.65rem' }} />
                          {friend.streak > 0 && (
                            <Chip
                              icon={<WhatshotIcon sx={{ fontSize: '12px !important' }} />}
                              label={friend.streak}
                              size="small"
                              color="warning"
                              sx={{ height: 18, fontSize: '0.65rem' }}
                            />
                          )}
                          {friend.lastSubmissionAt && (
                            <Typography variant="caption" color="text.secondary">
                              {timeAgo(friend.lastSubmissionAt)}
                            </Typography>
                          )}
                          {!friend.lastSubmissionAt && (
                            <Typography variant="caption" color="text.disabled">never active</Typography>
                          )}
                        </Box>
                      }
                      disableTypography
                    />
                  </ListItem>
                </Grid>
              ))}
            </Grid>
          )
        }
      </Paper>
    </Box>
  )
}
