import React from 'react'
import { useParams } from 'react-router-dom'
import { Alert, Box, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import PlayButton from '../components/ui/PlayButton'
import useRequireAuth from '../hooks/useRequireAuth'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'
import useGame from '../hooks/useGame'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'

export default function Submit() {
  const { gameId } = useParams()
  const { user } = useRequireAuth()
  const { game, hasSubmittedForLatest, notFound, error, refresh } = useGame(gameId)

  if (notFound) return <NotFound message="Game not found" />
  if (error && !game) {
    return (
      <Alert severity="error" sx={{ m: 2 }} action={<AppButton color="inherit" size="small" onClick={refresh}>Retry</AppButton>}>
        Could not load this game.
      </Alert>
    )
  }
  if (!game) return <Loading />

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h5">Submit for {game.name}</Typography>
        {game.url && (
          <Box sx={{ mt: 0.75 }}>
            <PlayButton game={game} />
          </Box>
        )}
      </Box>

      <SubmissionForm gameId={gameId} hasSubmitted={Boolean(user?.id && hasSubmittedForLatest)} />
    </Box>
  )
}
