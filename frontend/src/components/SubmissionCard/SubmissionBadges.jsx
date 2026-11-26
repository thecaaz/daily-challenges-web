import React from 'react'
import { Tooltip, Box, Chip } from '@mui/material'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import PersonIcon from '@mui/icons-material/Person'
import { useAuth } from '../../contexts/AuthContext'
import { winnerGold } from '../../styles/tokens'
import SubmitterName from './SubmitterName'

const rowSx = { display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 0.5 }
const nameGroupSx = { display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap' }
const winnerIconSx = { color: winnerGold, fontSize: '1.1rem' }
const ownChipIconSx = { fontSize: '0.8rem !important' }
const ownChipSx = { height: 18, fontSize: '0.65rem', fontWeight: 700 }
const rankChipSx = { fontWeight: 700, fontSize: '0.7rem' }

/** Submitter name with the winner, "You" and rank badges. */
export default function SubmissionBadges({ submission }) {
  const { user } = useAuth()
  const isOwnSubmission = Boolean(submission.userId && user && submission.userId === user.id)
  const winnerTitle = submission.scoringDay ? `Winner for ${submission.scoringDay}` : 'Winner'
  const hasRank = submission.rank != null

  return (
    <Box sx={rowSx}>
      <Box sx={nameGroupSx}>
        <SubmitterName submission={submission} />
        {submission.isDayWinner && (
          <Tooltip title={winnerTitle}>
            <EmojiEventsIcon sx={winnerIconSx} />
          </Tooltip>
        )}
        {isOwnSubmission && (
          <Chip icon={<PersonIcon sx={ownChipIconSx} />} label="You" size="small" color="primary" sx={ownChipSx} />
        )}
      </Box>
      {hasRank && <Chip label={`#${submission.rank}`} size="small" color="secondary" sx={rankChipSx} />}
    </Box>
  )
}
