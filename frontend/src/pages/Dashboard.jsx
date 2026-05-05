import React, { useEffect, useState, useCallback } from 'react'
import {
  Box, Typography, Grid, Paper, Skeleton, Chip, Divider,
  Avatar, List, ListItem, ListItemAvatar, ListItemText,
  Tooltip, Stack, LinearProgress
} from '@mui/material'
import { Link } from 'react-router-dom'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import PeopleIcon from '@mui/icons-material/People'
import TodayIcon from '@mui/icons-material/Today'
import SportsSoccerIcon from '@mui/icons-material/SportsSoccer'
import WhatshotIcon from '@mui/icons-material/Whatshot'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import BoltIcon from '@mui/icons-material/Bolt'
import api from '../api'
import useRequireAuth from '../hooks/useRequireAuth'
import AppButton from '../components/ui/AppButton'
import { hasAdapterForUrl } from '../utils/adapters'
import timeAgo from '../utils/timeAgo'
import GameRow from '../components/dashboard/GameRow'
import FriendActivityRow from '../components/dashboard/FriendActivityRow'
import RankRow from '../components/dashboard/RankRow'
import StatCard from '../components/dashboard/StatCard'
import Section from '../components/dashboard/Section'

// ─── Main page ────────────────────────────────────────────────────────────────

export default function Dashboard() {
  const { user, loading: authLoading } = useRequireAuth()

  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [favData, setFavData] = useState({ total: 0, submittedCount: 0, pendingGames: [], playableCount: 0 })

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

  // Compute favorites data: total, submitted today, pending list, and extension-playable count
  useEffect(() => {
    let mounted = true
    const computeFavData = async () => {
      try {
        const res = await api.get('/games')
        const all = Array.isArray(res.data) ? res.data : []
        const favs = all.filter(g => g.isFavorite)
        const pending = []
        let playableCount = 0
        for (const g of favs) {
          if (!g.hasSubmittedForLatest) pending.push(g)
          try {
            if (!g.url || g.hasSubmittedForLatest) continue
            const ok = await hasAdapterForUrl(g.url)
            if (ok) playableCount++
          } catch (e) {
            // ignore per-game
          }
        }
        if (mounted) setFavData({
          total: favs.length,
          submittedCount: favs.length - pending.length,
          pendingGames: pending,
          playableCount,
        })
      } catch (e) {
        if (mounted) setFavData({ total: 0, submittedCount: 0, pendingGames: [], playableCount: 0 })
      }
    }

    // Wait for auth to settle so server returns per-user flags
    if (authLoading) {
      return () => { mounted = false }
    }

    // If not authenticated, clear favorites summary
    if (!user) {
      if (mounted) setFavData({ total: 0, submittedCount: 0, pendingGames: [], playableCount: 0 })
      return () => { mounted = false }
    }

    computeFavData()

    const onFavChanged = () => {
      // re-compute favorites when toggled elsewhere
      computeFavData().catch(() => {})
    }
    window.addEventListener('favorite-changed', onFavChanged)

    return () => {
      mounted = false
      window.removeEventListener('favorite-changed', onFavChanged)
    }
  }, [authLoading, user?.id])

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
        <Stack direction="row" sx={{ mb: 3, flexWrap: 'wrap', gap: 2, '& > *': { flex: '1 1 140px', maxWidth: { md: 280 } } }}>
          {[1, 2, 3].map(i => <Skeleton key={i} variant="rounded" width={150} height={64} />)}
        </Stack>
        <Grid container spacing={2}>
          {[1, 2].map(i => (
            <Grid item xs={12} sm={6} lg={4} key={i}>
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

  const { recentGames = [], friendActivity = [], friends = [], xpEarnedToday = 0, userTodayRanks = [] } = data || {}
  const totalActiveToday = recentGames.reduce((sum, a) => sum + (a.todayCount || 0), 0)

  return (
    <Box>

      {/* Stat summary strip */}
      <Stack direction="row" sx={{ mb: 3, flexWrap: 'wrap', gap: 2, '& > *': { flex: '1 1 140px', maxWidth: { md: 280 } } }}>
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
        {xpEarnedToday > 0 && (
          <StatCard
            icon={<BoltIcon fontSize="small" />}
            label="XP earned today"
            value={`+${xpEarnedToday}`}
            color="warning"
          />
        )}

        <Box sx={{ alignContent: 'end', mb: 3, display: 'flex', alignItems: 'flex-start' }}>
          <Box sx={{ flex: 1 }} />
          <AppButton to="/dashboard/play" variant="contained" disabled={favData.playableCount === 0} sx={{ mt: 0.5 }}>Play ({favData.playableCount})</AppButton>
        </Box>
      </Stack>

      {/* Favorites completion progress */}
      {favData.total > 0 && (
        <Paper elevation={0} sx={{ p: 2, mb: 2, borderRadius: 3, border: '1px solid', borderColor: 'divider' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 1, gap: 1 }}>
            <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
            <Typography variant="subtitle2" fontWeight={700}>Today's Favorites</Typography>
            <Box sx={{ flex: 1 }} />
            <Typography variant="body2" color="text.secondary">
              {favData.submittedCount} / {favData.total} done
            </Typography>
          </Box>
          <LinearProgress
            variant="determinate"
            value={favData.total > 0 ? (favData.submittedCount / favData.total) * 100 : 0}
            color={favData.submittedCount === favData.total ? 'success' : 'primary'}
            sx={{ borderRadius: 2, height: 8 }}
          />
        </Paper>
      )}

      {/* Main grid: responsive – 1 col mobile, 2 col tablet, 3 col large */}
      <Grid container spacing={2} sx={{ mb: 2 }}>

        {/* Recent Games */}
        <Grid item xs={12} sm={6} lg={4}>
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
        <Grid item xs={12} sm={6} lg={4}>
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

        {/* Pending Favorites */}
        {favData.pendingGames.length > 0 && (
          <Grid item xs={12} sm={6} lg={4}>
            <Section
              title="Pending Favorites"
              icon={<StarBorderIcon fontSize="small" sx={{ color: 'warning.main' }} />}
              empty={false}
            >
              {favData.pendingGames.map(game => (
                <GameRow key={game.id} game={game} />
              ))}
            </Section>
          </Grid>
        )}

        {/* Your Ranks Today */}
        {userTodayRanks.length > 0 && (
          <Grid item xs={12} sm={6} lg={4}>
            <Section
              title="Your Ranks Today"
              icon={<EmojiEventsIcon fontSize="small" color="warning" />}
              empty={false}
            >
              {userTodayRanks.map(r => (
                <RankRow key={r.gameId} rank={r} />
              ))}
            </Section>
          </Grid>
        )}

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
                <Grid item xs={12} sm={6} md={4} lg={3} key={friend.userId}>
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
