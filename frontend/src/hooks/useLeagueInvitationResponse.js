import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * Accept or decline the current user's pending league invitations, one at a
 * time. Accepting opens the league; declining (or a failed accept) calls
 * `onInvitationsChanged` so the list can reload.
 */
export default function useLeagueInvitationResponse(onInvitationsChanged) {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const [isResponding, setIsResponding] = useState(false)

  const respondExclusively = async task => {
    if (isResponding) return
    setIsResponding(true)
    try {
      await task()
    } finally {
      setIsResponding(false)
    }
  }

  const accept = invitation => respondExclusively(async () => {
    try {
      await api.post(`/leagues/invitations/${invitation.id}/accept`)
      showSnackbar(`You joined "${invitation.leagueName}"!`)
      navigate(`/leagues/${invitation.leagueId}`)
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to accept invitation.'), 'error')
      onInvitationsChanged()
    }
  })

  const decline = invitation => respondExclusively(async () => {
    try {
      await api.post(`/leagues/invitations/${invitation.id}/decline`)
      showSnackbar('Invitation declined.')
      onInvitationsChanged()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to decline invitation.'), 'error')
    }
  })

  return { accept, decline, isResponding }
}
