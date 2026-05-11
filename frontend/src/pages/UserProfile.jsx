import React from 'react'
import { useParams } from 'react-router-dom'
import { Container, Box, Typography } from '@mui/material'
import useUserProfile from '../hooks/useUserProfile'
import UserProfileHeader from '../components/Profile/UserProfileHeader'
import TopGamesList from '../components/Profile/TopGamesList'
import AchievementsGrid from '../components/Profile/AchievementsGrid'
import Loading from '../components/ui/Loading'

export default function UserProfile() {
  const { id } = useParams()
  const userId = Number(id)
  const { data: profile, loading, error } = useUserProfile(Number.isNaN(userId) ? null : userId)

  if (loading) return <Container sx={{ mt: 4 }}><Loading /></Container>
  if (error) return <Container sx={{ mt: 4 }}><Box>Failed to load profile.</Box></Container>
  if (!profile) return <Container sx={{ mt: 4 }}><Box>Profile not found.</Box></Container>

  return (
    <Container sx={{ mt: 4 }}>
      <UserProfileHeader profile={profile} />
      <Box mt={3}>
        <AchievementsGrid userId={userId} />
      </Box>
      <Box mt={3}>
        <Typography variant="h6" gutterBottom>Top games</Typography>
        <TopGamesList games={profile.topGames || profile.TopGames || []} />
      </Box>
    </Container>
  )
}
