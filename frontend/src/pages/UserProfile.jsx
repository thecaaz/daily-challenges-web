import React from 'react'
import { useParams } from 'react-router-dom'
import { Container, Box, Typography, Alert } from '@mui/material'
import useUserProfile from '../hooks/useUserProfile'
import UserProfileHeader from '../components/Profile/UserProfileHeader'
import TopGamesList from '../components/Profile/TopGamesList'
import AchievementsGrid from '../components/Profile/AchievementsGrid'
import Loading from '../components/ui/Loading'
import NotFound from '../components/ui/NotFound'

export default function UserProfile() {
  const { id } = useParams()
  const userId = Number(id)
  const { data: profile, loading, error } = useUserProfile(Number.isNaN(userId) ? null : userId)

  if (loading) return <Container sx={{ mt: 4 }}><Loading /></Container>
  if (Number.isNaN(userId) || error?.response?.status === 404) {
    return <Container sx={{ mt: 4 }}><NotFound message="User not found." /></Container>
  }
  if (error) return <Container sx={{ mt: 4 }}><Box role="alert">Failed to load profile. Please try again later.</Box></Container>
  if (!profile) return <Container sx={{ mt: 4 }}><NotFound message="User not found." /></Container>

  return (
    <Container sx={{ mt: 4 }}>
      <UserProfileHeader profile={profile} />
      <Box mt={3}>
        <AchievementsGrid userId={userId} />
      </Box>
      <Box mt={3}>
        <Typography variant="h6" gutterBottom>Top games</Typography>
        {profile.scoresHidden && (
          <Alert severity="info" sx={{ mb: 2 }}>
            Today's scores are hidden and not included here until the day is over.
          </Alert>
        )}
        <TopGamesList games={profile.topGames || []} />
      </Box>
    </Container>
  )
}
