import React from 'react'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import LocalFireDepartmentIcon from '@mui/icons-material/LocalFireDepartment'
import StarIcon from '@mui/icons-material/Star'
import GroupIcon from '@mui/icons-material/Group'
import SendIcon from '@mui/icons-material/Send'
import MilitaryTechIcon from '@mui/icons-material/MilitaryTech'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import WhatshotIcon from '@mui/icons-material/Whatshot'
import BoltIcon from '@mui/icons-material/Bolt'

const ICON_MAP = {
  submit_first:  <SendIcon />,
  submit_50:     <StarIcon />,
  submit_250:    <AutoAwesomeIcon />,
  streak_7:      <LocalFireDepartmentIcon />,
  streak_30:     <WhatshotIcon />,
  streak_100:    <BoltIcon />,
  win_1:         <EmojiEventsIcon />,
  win_10:        <MilitaryTechIcon />,
  win_50:        <MilitaryTechIcon sx={{ color: 'gold' }} />,
  level_5:       <StarIcon />,
  level_10:      <StarIcon sx={{ color: 'silver' }} />,
  level_25:      <StarIcon sx={{ color: 'gold' }} />,
  first_friend:  <GroupIcon />,
}

/**
 * Returns a React element for the given achievement icon key,
 * or null if no mapping exists.
 * @param {string} iconKey
 * @returns {React.ReactElement | null}
 */
export function achievementIcon(iconKey) {
  return ICON_MAP[iconKey] ?? null
}
