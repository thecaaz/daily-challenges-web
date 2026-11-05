import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import AuthForm from '../components/ui/AuthForm'
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
      icon="🎮"
      title="Welcome back!"
      subtitle="Log in to track your scores and climb the leaderboard."
      submitLabel="Login"
      submitLoadingLabel="Logging in…"
      linkPrefix="No account?"
      linkText="Register here"
      linkTo={withRedirect('/register', redirect)}
      onSubmit={handleSubmit}
    />
  )
}
