import React from 'react'
import { Chip } from '@mui/material'
import GameListRow from './GameListRow'

const GAME_ROW_LOOK = { avatarColor: 'primary.dark', titleMaxWidth: 160, titleRowSx: { flexWrap: 'wrap' } }

export default function GameRow({ game, badge, badgeLabel }) {
  const hasBadge = badge !== undefined

  return (
    <GameListRow game={game} look={GAME_ROW_LOOK}>
      {hasBadge && (
        <Chip
          label={badgeLabel}
          size="small"
          color="primary"
          variant="outlined"
          sx={{ height: 18, fontSize: '0.65rem', mt: 0.25 }}
        />
      )}
    </GameListRow>
  )
}
