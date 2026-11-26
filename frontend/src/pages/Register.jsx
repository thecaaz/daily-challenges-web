import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import AuthForm from '../components/ui/AuthForm'
import { NEW_ACCOUNT_CREDENTIALS } from '../utils/credentialRules'
import { getApiErrorMessage } from '../utils/apiError'
import { getSafeRedirect, withRedirect } from '../utils/safeRedirect'

export default function Register() {
  const { register } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const redirect = getSafeRedirect(searchParams.get('redirect'), '/')
  const loginPath = withRedirect('/login', redirect)

  const handleSubmit = async (username, password) => {
    try {
      await register(username, password)
      showSnackbar('Account created! Please log in.', 'success')
      navigate(loginPath)
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Registration failed — username may already be taken'), 'error')
    }
  }

  return (
    <AuthForm
      heading={{ icon: '🏆', title: 'Join the challenge!', subtitle: 'Create an account to submit scores and compete with others.' }}
      submitLabels={{ idle: 'Register', loading: 'Creating account…' }}
      alternative={{ prefix: 'Already have an account?', text: 'Log in', to: loginPath }}
      credentials={NEW_ACCOUNT_CREDENTIALS}
      onSubmit={handleSubmit}
    />
  )
}
