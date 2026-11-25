import React from 'react'
import { Paper, Table, TablePagination, TableHead, TableRow, TableCell, TableBody } from '@mui/material'
import AdminUserRow from './AdminUserRow'

const PAGE_SIZE_OPTIONS = [25, 50, 100]
const COLUMNS = ['ID', 'Username', 'Total XP', 'Level', 'XP To Next', 'Streak', 'Actions']

/**
 * Paged table of users for admins.
 *
 * @param {object} props
 * @param {object} props.userList - state returned by useAdminUsers.
 * @param {number|undefined} props.currentUserId
 * @param {object} props.xp - state returned by useXpAdjustment.
 * @param {object} props.actions - row actions, see AdminUserRow.
 */
export default function AdminUsersTable({ userList, currentUserId, xp, actions }) {
  const { users, loading, error, page, setPage, pageSize, changePageSize, totalCount } = userList
  const isEmpty = !loading && users.length === 0
  const canDelete = user => !user.isAdmin && user.id !== currentUserId

  return (
    <Paper sx={{ p: 1.5 }}>
      <Table>
        <TableHead>
          <TableRow>
            {COLUMNS.map(column => <TableCell key={column}>{column}</TableCell>)}
          </TableRow>
        </TableHead>
        <TableBody>
          {users.map(user => (
            <AdminUserRow key={user.id} user={user} canDelete={canDelete(user)} xp={xp} actions={actions} />
          ))}
          {isEmpty && (
            <TableRow>
              <TableCell colSpan={COLUMNS.length}>{error ? 'Failed to load users.' : 'No users found.'}</TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
      <TablePagination
        component="div"
        count={totalCount}
        page={Math.max(0, page - 1)}
        onPageChange={(_, zeroBasedPage) => setPage(zeroBasedPage + 1)}
        rowsPerPage={pageSize}
        rowsPerPageOptions={PAGE_SIZE_OPTIONS}
        onRowsPerPageChange={event => changePageSize(Number(event.target.value))}
      />
    </Paper>
  )
}
