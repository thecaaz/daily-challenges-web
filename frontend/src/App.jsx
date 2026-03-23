import React from 'react'
import { Routes, Route, Link } from 'react-router-dom'
import { Container, Button, Typography } from '@mui/material'
import Games from './pages/Games'
import Admin from './pages/Admin'
import Submit from './pages/Submit'
import GameSubmissions from './pages/GameSubmissions'
import GameHighscore from './pages/GameHighscore'
import GameBar from './components/GameBar'
import Login from './pages/Login'
import Register from './pages/Register'

export default function App() {
  return (
    <div>
      <GameBar />
      <Container sx={{ mt: 4 }}>
        <Routes>
          <Route path="/" element={<Games />} />
          <Route path="/admin" element={<Admin />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/submit/:gameId" element={<Submit />} />
          <Route path="/games/:gameId" element={<GameSubmissions />} />
          <Route path="/games/:gameId/highscore" element={<GameHighscore />} />
        </Routes>
      </Container>
    </div>
  )
}
