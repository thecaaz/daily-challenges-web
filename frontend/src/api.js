import axios from 'axios'

const backendBase = (typeof window !== 'undefined' && window.__BACKEND_URL)
  ? window.__BACKEND_URL
  : 'http://localhost:5000'

const api = axios.create({
  baseURL: `${backendBase.replace(/\/$/, '')}/api`,
  withCredentials: true,
})

export default api
