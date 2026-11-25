import React from 'react'
import { Chip } from '@mui/material'
import NotificationsIcon from '@mui/icons-material/Notifications'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import GroupsIcon from '@mui/icons-material/Groups'
import MilitaryTechIcon from '@mui/icons-material/MilitaryTech'
import { winnerGold } from '../../styles/tokens'
import { ACHIEVEMENT_TYPE, DAY_WIN_TYPE, isFriendType, isLeagueType } from './notificationTypes'

const iconSx = { fontSize: '1.2rem' }
const winnerIconSx = { color: winnerGold, fontSize: '1.3rem' }
const rankChipSx = { fontWeight: 700, fontSize: '0.65rem', height: 20 }

/** Trophy for a day win, the rank for a placement, otherwise an icon per type. */
export default function NotificationIcon({ n }) {
  if (n.type === DAY_WIN_TYPE) return <EmojiEventsIcon sx={winnerIconSx} />
  if (n.rank != null) return <Chip label={`#${n.rank}`} size="small" sx={rankChipSx} />
  if (isFriendType(n.type)) return <PersonAddIcon color="action" sx={iconSx} />
  if (isLeagueType(n.type)) return <GroupsIcon color="action" sx={iconSx} />
  if (n.type === ACHIEVEMENT_TYPE) return <MilitaryTechIcon color="action" sx={iconSx} />
  return <NotificationsIcon color="action" sx={iconSx} />
}
