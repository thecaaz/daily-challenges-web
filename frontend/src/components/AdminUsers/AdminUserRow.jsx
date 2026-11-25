import React from 'react'
import { Box, TableRow, TableCell, TextField } from '@mui/material'
import { Link } from 'react-router-dom'
import AppButton from '../ui/AppButton'
import formatNumber from '../../utils/formatNumber'

const spacedButtonStyle = { marginLeft: 8 }

function UsernameCell({ user }) {
  if (!user.id) return <TableCell>{user.username}</TableCell>
  return (
    <TableCell>
      <Box component={Link} to={`/users/${user.id}`} sx={{ textDecoration: 'none', color: 'inherit' }}>{user.username}</Box>
    </TableCell>
  )
}

/**
 * One user in the admin list with XP adjustment and account actions.
 *
 * @param {object} props
 * @param {object} props.user
 * @param {boolean} props.canDelete
 * @param {object} props.xp - state returned by useXpAdjustment.
 * @param {{ openAudit: Function, openSetPassword: Function, deleteUser: Function }} props.actions
 */
export default function AdminUserRow({ user, canDelete, xp, actions }) {
  const hasValidAmount = xp.isValid(user.id)

  return (
    <TableRow>
      <TableCell>{user.id}</TableCell>
      <UsernameCell user={user} />
      <TableCell>{formatNumber(user.totalXp)}</TableCell>
      <TableCell>{user.level}</TableCell>
      <TableCell>{formatNumber(user.xpToNextLevel)}</TableCell>
      <TableCell>{user.streak}</TableCell>
      <TableCell style={{ whiteSpace: 'nowrap' }}>
        <TextField
          type="number"
          size="small"
          value={xp.amountFor(user.id)}
          onChange={event => xp.setAmount(user.id, event.target.value)}
          error={xp.isInvalidInput(user.id)}
          inputProps={{ step: 1, min: 1, 'aria-label': `XP amount for ${user.username}` }}
          style={{ width: 100, marginRight: 8 }}
        />
        <AppButton variant="contained" disabled={!hasValidAmount} onClick={() => xp.add(user.id)}>Add</AppButton>
        <AppButton variant="outlined" color="error" disabled={!hasValidAmount} onClick={() => xp.deduct(user.id)} style={spacedButtonStyle}>Deduct</AppButton>
        <AppButton variant="text" onClick={() => actions.openAudit(user)} style={spacedButtonStyle}>Audit</AppButton>
        <AppButton variant="outlined" onClick={() => actions.openSetPassword(user.id)} style={spacedButtonStyle}>Set Password</AppButton>
        {canDelete && (
          <AppButton variant="outlined" color="error" onClick={() => actions.deleteUser(user.id)} style={spacedButtonStyle}>Delete</AppButton>
        )}
      </TableCell>
    </TableRow>
  )
}
