import React, { useState } from 'react'
import { InputAdornment, CircularProgress, TextField, Typography, Box } from '@mui/material'
import SearchIcon from '@mui/icons-material/Search'
import useRequireAdmin from '../hooks/useRequireAdmin'
import useConfirm from '../hooks/useConfirm'
import usePrompt from '../hooks/usePrompt'
import useAdminUsers from '../hooks/useAdminUsers'
import useXpAdjustment from '../hooks/useXpAdjustment'
import useSetPasswordDialog from '../hooks/useSetPasswordDialog'
import useDeleteUser from '../hooks/useDeleteUser'
import AdminUserAuditModal from '../components/AdminUserAuditModal'
import AdminUsersTable from '../components/AdminUsers/AdminUsersTable'
import SetPasswordDialog from '../components/AdminUsers/SetPasswordDialog'
import ConfirmDialog from '../components/ui/ConfirmDialog'
import PromptDialog from '../components/ui/PromptDialog'

export { parseXpAmount } from '../hooks/useXpAdjustment'

export default function AdminUsers() {
  const { user, isAuthorized } = useRequireAdmin()
  const { confirm, dialogProps } = useConfirm()
  const { prompt, dialogProps: promptDialogProps } = usePrompt()
  const userList = useAdminUsers(isAuthorized)
  const xp = useXpAdjustment({ prompt, onAdjusted: userList.refresh })
  const passwordDialog = useSetPasswordDialog()
  const deleteUser = useDeleteUser({ confirm, onDeleted: userList.refresh })
  const [auditedUser, setAuditedUser] = useState(null)
  const [isAuditOpen, setIsAuditOpen] = useState(false)

  const openAudit = selectedUser => {
    setAuditedUser(selectedUser)
    setIsAuditOpen(true)
  }

  if (!isAuthorized) return null

  const rowActions = { openAudit, openSetPassword: passwordDialog.open, deleteUser }

  return (
    <>
      <Typography variant="h5" sx={{ mb: 2 }}>Admin — Manage Users</Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <TextField
          size="small"
          label="Search username"
          value={userList.searchInput}
          onChange={event => userList.setSearchInput(event.target.value)}
          InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }}
        />
        {userList.loading && <CircularProgress size={20} />}
      </Box>
      <AdminUsersTable userList={userList} currentUserId={user?.id} xp={xp} actions={rowActions} />

      <SetPasswordDialog passwordDialog={passwordDialog} />
      <AdminUserAuditModal
        open={isAuditOpen}
        onClose={() => setIsAuditOpen(false)}
        userId={auditedUser?.id ?? null}
        username={auditedUser?.username ?? null}
      />
      <ConfirmDialog {...dialogProps} />
      <PromptDialog {...promptDialogProps} />
    </>
  )
}
