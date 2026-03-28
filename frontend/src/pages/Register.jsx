import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useSnackbar } from '../contexts/SnackbarContext'

export default function Register(){
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const { register } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const submit = async (e) => {
    e.preventDefault()
    try {
      await register(username, password)
      navigate('/login')
    } catch (err) {
      showSnackbar('Registration failed', 'error')
    }
  }

  return (
    <form onSubmit={submit} style={{ maxWidth: 400 }}>
      <h2>Register</h2>
      <div>
        <input placeholder="Username" value={username} onChange={e=>setUsername(e.target.value)} />
      </div>
      <div>
        <input placeholder="Password" type="password" value={password} onChange={e=>setPassword(e.target.value)} />
      </div>
      <button type="submit">Register</button>
    </form>
  )
}
