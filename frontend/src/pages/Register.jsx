import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'
import AuthForm from '../components/ui/AuthForm'

export default function Register() {
  const { register } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const handleSubmit = async (username, password) => {
    try {
      await register(username, password)
      showSnackbar('Account created! Please log in.', 'success')
      navigate('/login')
    } catch (err) {
      showSnackbar('Registration failed — username may already be taken', 'error')
    }
  }

  return (
    <AuthForm
      icon="🏆"
      title="Join the challenge!"
      subtitle="Create an account to submit scores and compete with others."
      submitLabel="Register"
      submitLoadingLabel="Creating account…"
      passwordAutoComplete="new-password"
      linkPrefix="Already have an account?"
      linkText="Log in"
      linkTo="/login"
      onSubmit={handleSubmit}
    />
  )
}
