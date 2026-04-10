import React from 'react'
import { Routes, Route } from 'react-router-dom'
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
import TimeguessrDebug from './pages/TimeguessrDebug'
import Play from './pages/Play'
import DashboardPlay from './pages/DashboardPlay'

export default function App() {
  return (
    <div>
      <GameBar />
      <Routes>
        <Route path="/compare/:id1/:id2" element={<Box sx={{ mt: 4, mb: 6, px: 3 }}><CompareSubmissions /></Box>} />
        <Route path="*" element={
          <Container sx={{ mt: 4, mb: 6 }}>
            <Routes>
              <Route path="/" element={<Games />} />
              <Route path="/dashboard" element={<Dashboard />} />
              <Route path="/dashboard/play" element={<DashboardPlay />} />
              <Route path="/admin" element={<Admin />} />
              <Route path="/admin/users" element={<AdminUsers />} />
              <Route path="/debug/timeguessr" element={<TimeguessrDebug />} />
              <Route path="/play/:gameId" element={<Play />} />
              <Route path="/login" element={<Login />} />
              <Route path="/register" element={<Register />} />
              <Route path="/submit/:gameId" element={<Submit />} />
              <Route path="/submission/:id" element={<SubmissionDetail />} />
              <Route path="/games/:gameId" element={<GameSubmissions />} />
              <Route path="/games/:gameId/highscore" element={<GameHighscore />} />
              <Route path="/games/:gameId/personal-highscore" element={<PersonalHighscore />} />
              <Route path="/users/:id" element={<UserProfile />} />
            </Routes>
          </Container>
        } />
      </Routes>
    </div>
  )
}
