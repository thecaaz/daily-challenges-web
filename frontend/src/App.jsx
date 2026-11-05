import React, { Suspense, lazy } from 'react'
import { Routes, Route, useLocation } from 'react-router-dom'
import { Container, Box } from '@mui/material'
import Games from './pages/Games'
import Dashboard from './pages/Dashboard'
import Admin from './pages/Admin'
import AdminUsers from './pages/AdminUsers'
import Submit from './pages/Submit'
import SubmissionDetail from './pages/SubmissionDetail'
import CompareSubmissions from './pages/CompareSubmissions'
import GameSubmissions from './pages/GameSubmissions'
import GameHighscore from './pages/GameHighscore'
import PersonalHighscore from './pages/PersonalHighscore'
import GameBar from './components/GameBar'
import Login from './pages/Login'
import Register from './pages/Register'
import UserProfile from './pages/UserProfile'
import Play from './pages/Play'
import DashboardPlay from './pages/DashboardPlay'
import Extension from './pages/Extension'
import Friends from './pages/Friends'
import Leagues from './pages/Leagues'
import LeagueDetail from './pages/LeagueDetail'
import LeagueJoin from './pages/LeagueJoin'
import ErrorBoundary from './components/ErrorBoundary'
import NotFound from './components/ui/NotFound'
import Loading from './components/ui/Loading'
import { useAuth } from './contexts/AuthContext'

// Dev/admin-only tool: keep it (and its adapter-debug code) out of the main bundle.
const TimeguessrDebug = lazy(() => import('./pages/TimeguessrDebug'))

export default function App() {
  const location = useLocation()
  const { user } = useAuth()
  // The Timeguessr debug page is a dev/admin tool only.
  const showDebugRoute = import.meta.env.DEV || !!user?.isAdmin

  return (
    <div>
      <GameBar />
      <ErrorBoundary resetKey={location.pathname}>
      <Routes>
        <Route path="/compare/:id1/:id2" element={<Box sx={{ mt: 4, mb: 6, px: 3 }}><CompareSubmissions /></Box>} />
        <Route path="/dashboard/play" element={<Box sx={{ mt: 4, mb: 6, px: { xs: 2, sm: 3, md: 4 } }}><DashboardPlay /></Box>} />
        <Route path="/dashboard" element={<Box sx={{ mt: 4, mb: 6, px: { xs: 2, sm: 3, md: 4 } }}><Dashboard /></Box>} />
        <Route path="/" element={<Box sx={{ mt: 4, mb: 6, px: { xs: 2, sm: 3, md: 4 } }}><Dashboard /></Box>} />
        <Route path="*" element={
          <Container sx={{ mt: 4, mb: 6 }}>
            <Routes>
              <Route path="/games" element={<Games />} />
              <Route path="/admin" element={<Admin />} />
              <Route path="/admin/users" element={<AdminUsers />} />
              <Route path="/debug/timeguessr" element={showDebugRoute ? <Suspense fallback={<Loading />}><TimeguessrDebug /></Suspense> : <NotFound />} />
              <Route path="/play/:gameId" element={<Play />} />
              <Route path="/login" element={<Login />} />
              <Route path="/register" element={<Register />} />
              <Route path="/submit/:gameId" element={<Submit />} />
              <Route path="/submission/:id" element={<SubmissionDetail />} />
              <Route path="/games/:gameId" element={<GameSubmissions />} />
              <Route path="/games/:gameId/highscore" element={<GameHighscore />} />
              <Route path="/games/:gameId/personal-highscore" element={<PersonalHighscore />} />
              <Route path="/users/:id" element={<UserProfile />} />
              <Route path="/friends" element={<Friends />} />
              <Route path="/leagues" element={<Leagues />} />
              <Route path="/leagues/:id" element={<LeagueDetail />} />
              <Route path="/leagues/join/:token" element={<LeagueJoin />} />
              <Route path="/extension" element={<Extension />} />
            </Routes>
          </Container>
        } />
      </Routes>
      </ErrorBoundary>
    </div>
  )
}
