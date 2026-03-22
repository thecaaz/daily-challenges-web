import React from 'react'
import { Routes, Route, Link } from 'react-router-dom'
import { Container, AppBar, Toolbar, Button, Typography } from '@mui/material'
import Games from './pages/Games'
import Admin from './pages/Admin'
import Submit from './pages/Submit'

export default function App() {
  return (
    <div>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>Daily Challenges</Typography>
          <Button color="inherit" component={Link} to="/">Home</Button>
          <Button color="inherit" component={Link} to="/admin">Admin</Button>
        </Toolbar>
      </AppBar>
      <Container sx={{ mt: 4 }}>
        <Routes>
          <Route path="/" element={<Games />} />
          <Route path="/admin" element={<Admin />} />
          <Route path="/submit/:gameId" element={<Submit />} />
        </Routes>
      </Container>
    </div>
  )
}
