import React from 'react'
import { Grid } from '@mui/material'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import TodayIcon from '@mui/icons-material/Today'
import WhatshotIcon from '@mui/icons-material/Whatshot'
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents'
import GameRow from './GameRow'
import FriendActivityRow from './FriendActivityRow'
import RankRow from './RankRow'
import Section from './Section'

const playersTodayLabel = count => `${count} player${count !== 1 ? 's' : ''} today`

function SectionCell({ children }) {
  return <Grid item xs={12} sm={6} lg={4}>{children}</Grid>
}

/** Responsive grid of dashboard lists: 1 column on mobile, 2 on tablet, 3 on large screens. */
export default function DashboardSections({ summary, pendingFavorites }) {
  const { recentGames, friendActivity, friends, userTodayRanks } = summary
  const hasPendingFavorites = pendingFavorites.length > 0
  const hasRanksToday = userTodayRanks.length > 0
  const friendActivityEmptyText = friends.length === 0
    ? "Add friends to see what they're playing."
    : 'No friend activity in the last 7 days.'

  return (
    <Grid container spacing={2} sx={{ mb: 2 }}>
      <SectionCell>
        <Section
          title="Active Today"
          icon={<TodayIcon fontSize="small" color="primary" />}
          empty={recentGames.length === 0}
          emptyText="No games have submissions today yet."
        >
          {recentGames.map(activity => (
            <GameRow
              key={activity.game.id}
              game={activity.game}
              badge={activity.todayCount}
              badgeLabel={playersTodayLabel(activity.todayCount)}
            />
          ))}
        </Section>
      </SectionCell>

      <SectionCell>
        <Section
          title="Friends' Activity"
          icon={<WhatshotIcon fontSize="small" color="warning" />}
          empty={friendActivity.length === 0}
          emptyText={friendActivityEmptyText}
        >
          {friendActivity.map(activity => (
            <FriendActivityRow key={activity.gameId} activity={activity} />
          ))}
        </Section>
      </SectionCell>

      {hasPendingFavorites && (
        <SectionCell>
          <Section title="Pending Favorites" icon={<StarBorderIcon fontSize="small" sx={{ color: 'warning.main' }} />} empty={false}>
            {pendingFavorites.map(game => <GameRow key={game.id} game={game} />)}
          </Section>
        </SectionCell>
      )}

      {hasRanksToday && (
        <SectionCell>
          <Section title="Your Ranks Today" icon={<EmojiEventsIcon fontSize="small" color="warning" />} empty={false}>
            {userTodayRanks.map(rank => <RankRow key={rank.gameId} rank={rank} />)}
          </Section>
        </SectionCell>
      )}
    </Grid>
  )
}
