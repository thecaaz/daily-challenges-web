import React from 'react'
import { Table, TableHead, TableRow, TableCell, TableBody } from '@mui/material'
import { formatDateTimeUtc } from '../../utils/dateFormat'
import formatNumber from '../../utils/formatNumber'

const COLUMNS = ['When (UTC)', 'Amount', 'Type', 'Details', 'Submission', 'Game', 'Scoring Day']

/** scoringDay is a "yyyy-MM-dd" string (older backends sent a full DateTime). */
function formatScoringDay(value) {
  if (!value) return ''
  const text = String(value)
  return /^\d{4}-\d{2}-\d{2}/.test(text) ? text.slice(0, 10) : text
}

export default function XpEventsTable({ events }) {
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          {COLUMNS.map(column => <TableCell key={column}>{column}</TableCell>)}
        </TableRow>
      </TableHead>
      <TableBody>
        {events.map(event => (
          <TableRow key={event.id}>
            <TableCell>{formatDateTimeUtc(event.createdAt)}</TableCell>
            <TableCell>{formatNumber(event.amount)}</TableCell>
            <TableCell>{event.eventType}</TableCell>
            <TableCell style={{ maxWidth: 300, whiteSpace: 'pre-wrap' }}>{event.details}</TableCell>
            <TableCell>{event.submissionId ?? ''}</TableCell>
            <TableCell>{event.gameId ?? ''}</TableCell>
            <TableCell>{formatScoringDay(event.scoringDay)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
