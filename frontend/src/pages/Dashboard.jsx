import React from 'react'
import { Box, Alert } from '@mui/material'
import useRequireAuth from '../hooks/useRequireAuth'
import useDashboard from '../hooks/useDashboard'
import usePlayableFavorites from '../hooks/usePlayableFavorites'
import AppButton from '../components/ui/AppButton'
import DashboardSkeleton from '../components/dashboard/DashboardSkeleton'
import DashboardStats from '../components/dashboard/DashboardStats'
import FavoritesProgress from '../components/dashboard/FavoritesProgress'
import DashboardSections from '../components/dashboard/DashboardSections'
import FriendsPanel from '../components/dashboard/FriendsPanel'

export default function Dashboard() {
  const { user, loading: authLoading } = useRequireAuth()
  const favorites = usePlayableFavorites(user?.id, { enabled: !authLoading })
  const { summary, loading, error, retry } = useDashboard(user?.id)

  if (loading) return <DashboardSkeleton />

  if (error || !summary) {
    return (
      <Alert severity="error" action={<AppButton color="inherit" size="small" onClick={retry}>Retry</AppButton>}>
        Could not load the dashboard.
      </Alert>
    )
  }

  const hasFavorites = favorites.total > 0

  return (
    <Box>
      <DashboardStats summary={summary} favorites={favorites} />
      {hasFavorites && <FavoritesProgress submittedCount={favorites.submittedCount} total={favorites.total} />}
      <DashboardSections summary={summary} pendingFavorites={favorites.pendingGames} />
      <FriendsPanel friends={summary.friends} />
    </Box>
  )
}
