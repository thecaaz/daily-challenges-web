import React from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import AuthForm from '../components/ui/AuthForm'

export default function Login() {
  const { login } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const handleSubmit = async (username, password) => {
    try {
      await login(username, password)
      navigate('/')
    } catch (err) {
      showSnackbar('Login failed — check your username and password', 'error')
    }
  }

  return (
    <AuthForm
      icon="🎮"
      title="Welcome back!"
      subtitle="Log in to track your scores and climb the leaderboard."
      submitLabel="Login"
      submitLoadingLabel="Logging in…"
      linkPrefix="No account?"
      linkText="Register here"
      linkTo="/register"
      onSubmit={handleSubmit}
    />
  )
}
