import React, { useEffect, useState } from 'react'
import { Paper, Table, TableHead, TableRow, TableCell, TableBody, Button, TextField } from '@mui/material'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { useAuth } from '../contexts/AuthContext'
import { useNavigate } from 'react-router-dom'

export default function AdminUsers() {
  const { user, loading } = useAuth()
  const { showSnackbar } = useSnackbar()
  const navigate = useNavigate()

  const [users, setUsers] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(50)
  const [totalCount, setTotalCount] = useState(0)
  const [deltaById, setDeltaById] = useState({})

  useEffect(() => { fetchUsers() }, [])

  if (!loading && (!user || !user.isAdmin)) {
    navigate('/')
    return null
  }

  const fetchUsers = async () => {
    try {
      const res = await api.get('/admin/users', { params: { page, pageSize } })
      setUsers(res.data.items || [])
      setTotalCount(res.data.totalCount || 0)
    } catch (err) {
      showSnackbar('Failed to load users', 'error')
    }
  }

  const adjust = async (id, delta) => {
    const reason = window.prompt('Reason for adjustment (optional):') || ''
    try {
      const res = await api.post(`/admin/users/${id}/xp`, { delta, reason })
      showSnackbar('XP adjusted', 'success')
      // Update single row
      setUsers(users.map(u => u.id === res.data.id ? res.data : u))
    } catch (err) {
      showSnackbar('Failed to adjust XP', 'error')
    }
  }

  return (
    <>
      <h2>Admin — Manage Users</h2>
      <Paper style={{ padding: 12 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Username</TableCell>
              <TableCell>Total XP</TableCell>
              <TableCell>Level</TableCell>
              <TableCell>XP To Next</TableCell>
              <TableCell>Streak</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map(u => (
              <TableRow key={u.id}>
                <TableCell>{u.id}</TableCell>
                <TableCell>{u.username}</TableCell>
                <TableCell>{u.totalXp}</TableCell>
                <TableCell>{u.level}</TableCell>
                <TableCell>{u.xpToNextLevel}</TableCell>
                <TableCell>{u.streak}</TableCell>
                <TableCell>
                  <TextField
                    type="number"
                    size="small"
                    value={deltaById[u.id] ?? ''}
                    onChange={e => setDeltaById({ ...deltaById, [u.id]: e.target.value })}
                    style={{ width: 100, marginRight: 8 }}
                  />
                  <Button variant="contained" onClick={() => adjust(u.id, Math.abs(Number(deltaById[u.id] || 0)))}>Add</Button>
                  <Button variant="outlined" color="error" onClick={() => adjust(u.id, -Math.abs(Number(deltaById[u.id] || 0)))} style={{ marginLeft: 8 }}>Deduct</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  )
}
