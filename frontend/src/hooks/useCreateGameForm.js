import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import { localToUtcHHmm } from '../utils/resetTime'
import useImageUpload from './useImageUpload'

const EMPTY_GAME = { name: '', url: '', description: '', localResetTime: '00:00', rankingMode: 'highest' }

function buildGameFormData(fields, utcResetTime, image) {
  const formData = new FormData()
  formData.append('name', fields.name.trim())
  formData.append('url', fields.url.trim())
  formData.append('description', fields.description)
  formData.append('resetTime', utcResetTime)
  formData.append('rankingMode', fields.rankingMode)
  if (image) formData.append('image', image)
  return formData
}

/**
 * State and submit handler for the "create game" form. The reset time is
 * entered in local time and sent as UTC. On success the admin is sent back to
 * the games overview.
 *
 * @param {object} options
 * @param {boolean} options.acceptsPaste - whether a pasted image fills this form's image.
 */
export default function useCreateGameForm({ acceptsPaste }) {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const [fields, setFields] = useState(EMPTY_GAME)
  const image = useImageUpload(showSnackbar, { enabled: acceptsPaste })

  const setField = (field, value) => setFields(prev => ({ ...prev, [field]: value }))

  const submit = async (event) => {
    event.preventDefault()
    const utcResetTime = localToUtcHHmm(fields.localResetTime)
    if (!utcResetTime) {
      showSnackbar('Enter a valid reset time', 'error')
      return
    }
    try {
      await api.post('/games', buildGameFormData(fields, utcResetTime, image.screenshot))
      setFields(EMPTY_GAME)
      image.clear()
      showSnackbar('Game created', 'success')
      navigate('/')
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to create game'), 'error')
    }
  }

  return { fields, setField, submit, image }
}
