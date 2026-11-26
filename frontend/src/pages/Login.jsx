import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import AuthForm from '../components/ui/AuthForm'
import { LOGIN_CREDENTIALS } from '../utils/credentialRules'
import { getApiErrorMessage } from '../utils/apiError'
import { getSafeRedirect, withRedirect } from '../utils/safeRedirect'

export default function Login() {
  const { login } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const redirect = getSafeRedirect(searchParams.get('redirect'), '/')

  const handleSubmit = async (username, password) => {
    try {
      await login(username, password)
      navigate(redirect, { replace: true })
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Login failed — check your username and password'), 'error')
    }
  }

  return (
    <AuthForm
      heading={{ icon: '🎮', title: 'Welcome back!', subtitle: 'Log in to track your scores and climb the leaderboard.' }}
      submitLabels={{ idle: 'Login', loading: 'Logging in…' }}
      alternative={{ prefix: 'No account?', text: 'Register here', to: withRedirect('/register', redirect) }}
      credentials={LOGIN_CREDENTIALS}
      onSubmit={handleSubmit}
    />
  )
}
