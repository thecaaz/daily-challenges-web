import React, { useState, useEffect } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Box, Typography } from '@mui/material'
import AppButton from '../components/ui/AppButton'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import useRequireAuth from '../hooks/useRequireAuth'
import NotFound from '../components/ui/NotFound'
import Loading from '../components/ui/Loading'
import useGame from '../hooks/useGame'
import SubmissionForm from '../components/SubmissionForm/SubmissionForm'

export default function Submit() {
  const { gameId } = useParams()
  const [game, setGame] = useState(null)
  const [notFound, setNotFound] = useState(false)
  

  const { game: hookGame, hasSubmittedForLatest: hookHasSubmittedForLatest, notFound: hookNotFound } = useGame(gameId)

  useEffect(() => {
    if (hookNotFound) {
      setNotFound(true)
      return
    }
    if (hookGame) setGame(hookGame)
  }, [hookGame, hookNotFound])
  const { user, loading, fetchMe } = useRequireAuth()

  const [hasSubmitted, setHasSubmitted] = useState(false)

  // sync has-submitted state for authenticated users
  useEffect(() => {
    if (hookHasSubmittedForLatest && user && user.id) {
      setHasSubmitted(true)
    }
  }, [hookHasSubmittedForLatest, user])

  

  if (notFound) return <NotFound message="Game not found" />
  if (!game) return <Loading />

  return (
    <div>
      <Box sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
          <Typography variant="h5">Submit for {game.name}</Typography>
          {game.url && (
            <div style={{ marginTop: 6 }}>
              <AppButton
                to={`/play/${gameId}`}
                variant="outlined"
                size="small"
                color="primary"
                endIcon={<OpenInNewIcon />}
                dataTest="game-play-link"
              >
                Play
              </AppButton>
            </div>
          )}
        </Box>

        <SubmissionForm gameId={gameId} hasSubmitted={hasSubmitted} />
      </Box>
    </div>
  )
}
