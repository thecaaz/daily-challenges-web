import { useNavigate } from 'react-router-dom'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'

/**
 * Membership actions on a league: cancel an invitation, remove a member,
 * leave or delete the league. Destructive actions ask for confirmation first.
 *
 * @param {string|number} leagueId
 * @param {{ onLeagueChanged: () => void, onMembersChanged: () => void }} callbacks
 *   `onLeagueChanged` after any change to the league; `onMembersChanged` also
 *   after a member was removed.
 */
export default function useLeagueMembershipActions(leagueId, { onLeagueChanged, onMembersChanged }) {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()

  const showError = (err, fallback) => showSnackbar(getApiErrorMessage(err, fallback), 'error')

  const cancelInvitation = async invitationId => {
    try {
      await api.delete(`/leagues/invitations/${invitationId}`)
      showSnackbar('Invitation cancelled.')
      onLeagueChanged()
    } catch (err) {
      showError(err, 'Failed to cancel invitation.')
    }
  }

  const removeMember = async (memberId, username) => {
    if (!window.confirm(`Remove ${username} from this league?`)) return
    try {
      await api.delete(`/leagues/${leagueId}/members/${memberId}`)
      showSnackbar(`${username} removed.`)
      onLeagueChanged()
      onMembersChanged()
    } catch (err) {
      showError(err, 'Failed to remove member.')
    }
  }

  const leaveLeague = async () => {
    if (!window.confirm('Leave this league?')) return
    try {
      await api.post(`/leagues/${leagueId}/leave`)
      showSnackbar('You left the league.')
      navigate('/leagues')
    } catch (err) {
      showError(err, 'Failed to leave.')
    }
  }

  const deleteLeague = async () => {
    if (!window.confirm('Delete this league? This cannot be undone.')) return
    try {
      await api.delete(`/leagues/${leagueId}`)
      showSnackbar('League deleted.')
      navigate('/leagues')
    } catch (err) {
      showError(err, 'Failed to delete league.')
    }
  }

  return { cancelInvitation, removeMember, leaveLeague, deleteLeague }
}
