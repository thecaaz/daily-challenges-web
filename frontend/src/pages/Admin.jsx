import React from 'react'
import { Box } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import AppButton from '../components/ui/AppButton'
import ConfirmDialog from '../components/ui/ConfirmDialog'
import CreateGameForm from '../components/Admin/CreateGameForm'
import AdminGameList from '../components/Admin/AdminGameList'
import EditGameForm from '../components/Admin/EditGameForm'
import AdminSubmissionsTable from '../components/Admin/AdminSubmissionsTable'
import useRequireAdmin from '../hooks/useRequireAdmin'
import useConfirm from '../hooks/useConfirm'
import useAdminGames from '../hooks/useAdminGames'
import useGameEditor from '../hooks/useGameEditor'
import useAdminSubmissions from '../hooks/useAdminSubmissions'
import useDeleteGame from '../hooks/useDeleteGame'

export default function Admin() {
  const { isAuthorized } = useRequireAdmin()
  if (!isAuthorized) return null
  return <AdminDashboard />
}

function AdminDashboard() {
  const { confirm, dialogProps } = useConfirm()
  const { games, reload: reloadGames } = useAdminGames()
  const editor = useGameEditor({ onSaved: reloadGames })
  const submissions = useAdminSubmissions(confirm)
  const deleteGame = useDeleteGame({
    confirm,
    onDeleted: (gameId) => {
      editor.cancelIfEditing(gameId)
      submissions.closeIfShowing(gameId)
      reloadGames()
    },
  })

  const isShowingSubmissions = submissions.selectedGameId != null

  return (
    <>
      <AdminShortcuts />
      <CreateGameForm acceptsPaste={!editor.isEditing} />
      <AdminGameList
        games={games}
        onEdit={editor.start}
        onDelete={deleteGame}
        onManageSubmissions={submissions.show}
      />
      {editor.isEditing && <EditGameForm key={editor.editingGame.id} editor={editor} />}
      {isShowingSubmissions && (
        <AdminSubmissionsTable
          submissions={submissions.submissions}
          onScoreChange={submissions.setScore}
          onSave={submissions.save}
          onDelete={submissions.remove}
        />
      )}
      <ConfirmDialog {...dialogProps} />
    </>
  )
}

function AdminShortcuts() {
  const navigate = useNavigate()

  return (
    <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1.5 }}>
      <AppButton onClick={() => navigate('/admin/users')} sx={{ mb: 2, mr: 1 }}>Manage Users</AppButton>
      <AppButton onClick={() => navigate('/debug/timeguessr')} sx={{ mb: 2 }}>Debug Timeguessr Test</AppButton>
    </Box>
  )
}
