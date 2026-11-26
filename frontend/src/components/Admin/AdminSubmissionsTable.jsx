import React from 'react'
import { Paper, Table, TableHead, TableRow, TableCell, TableBody, Typography } from '@mui/material'
import AppButton from '../ui/AppButton'
import { formatDateTime } from '../../utils/dateFormat'
import { isNonNumericScore } from '../../utils/parseScore'
import { warningOrange } from '../../styles/tokens'
import UserLink from '../ui/UserLink'

const NOT_A_NUMBER_STYLE = { color: warningOrange, fontSize: '0.75rem', marginTop: 2 }

/**
 * Moderation table for one game's submissions: edit a score inline, then save
 * or delete the row.
 */
export default function AdminSubmissionsTable({ submissions, onScoreChange, onSave, onDelete }) {
  return (
    <Paper sx={{ p: 1.5, mt: 1.5 }}>
      <Typography variant="h6" sx={{ mb: 1 }}>Submissions</Typography>
      <Table>
        <TableHead>
          <TableRow>
            <TableCell>ID</TableCell>
            <TableCell>Username</TableCell>
            <TableCell>Score</TableCell>
            <TableCell>Created</TableCell>
            <TableCell></TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {submissions.map(submission => (
            <SubmissionRow
              key={submission.id}
              submission={submission}
              onScoreChange={onScoreChange}
              onSave={onSave}
              onDelete={onDelete}
            />
          ))}
        </TableBody>
      </Table>
    </Paper>
  )
}

function SubmissionRow({ submission, onScoreChange, onSave, onDelete }) {
  return (
    <TableRow>
      <TableCell>{submission.id}</TableCell>
      <TableCell><SubmitterName submission={submission} /></TableCell>
      <TableCell>
        <input value={submission.score ?? ''} onChange={e => onScoreChange(submission.id, e.target.value)} />
        {isNonNumericScore(submission.score) && <div style={NOT_A_NUMBER_STYLE}>Not a number</div>}
      </TableCell>
      <TableCell>{formatDateTime(submission.createdAt)}</TableCell>
      <TableCell>
        <AppButton onClick={() => onSave(submission)}>Save</AppButton>
        <AppButton color="error" onClick={() => onDelete(submission.id)}>Delete</AppButton>
      </TableCell>
    </TableRow>
  )
}

function SubmitterName({ submission }) {
  const username = submission.username ?? ''
  if (!submission.userId) return username

  return (
    <UserLink userId={submission.userId}>
      {username}
    </UserLink>
  )
}
