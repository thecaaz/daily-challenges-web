import React, { useEffect, useState } from 'react'
import { Stack, TextField, Alert, Box, Card, Typography } from '@mui/material'
import AppButton from '../ui/AppButton'
import ImageUpload from '../ui/ImageUpload/ImageUpload'
import useImageUpload from '../../hooks/useImageUpload'
import { useSnackbar } from '../../contexts/SnackbarContext'
import useRequireAuth from '../../hooks/useRequireAuth'
import useConfirm from '../../hooks/useConfirm'
import useSubmitScore from '../../hooks/useSubmitScore'
import ConfirmDialog from '../ui/ConfirmDialog'
import { NON_NUMERIC_SCORE_WARNING, findSubmissionProblem, isNonNumericScore, trimScore } from './submissionRules'

const FORM_MAX_WIDTH = 480
const NON_NUMERIC_CONFIRMATION = {
  title: 'Non-numeric score',
  message: `${NON_NUMERIC_SCORE_WARNING} Submit anyway?`,
  confirmText: 'Submit',
  confirmColor: 'primary',
}

/**
 * Score + screenshot submission for a game. `initialScore` and
 * `initialScreenshot` prefill the form (e.g. from an extension capture) and
 * are followed when they change. See useSubmitScore for `onSubmitted`.
 */
export default function SubmissionForm({ gameId, initialScore = '', initialScreenshot = null, hasSubmitted = false, onCancel, onSubmitted }) {
  const { showSnackbar } = useSnackbar()
  const { user } = useRequireAuth()
  const { confirm, dialogProps } = useConfirm()
  const { submit, isSubmitting } = useSubmitScore(gameId, onSubmitted)

  const [score, setScore] = useState(initialScore)
  const { screenshot, previewUrl, setScreenshot, onFileChange, clear } = useImageUpload(showSnackbar)

  useEffect(() => {
    if (initialScore !== undefined && initialScore !== score) setScore(initialScore)
  // eslint-disable-next-line react-hooks/exhaustive-deps -- only a new initialScore from the caller resets the field
  }, [initialScore])

  useEffect(() => {
    if (initialScreenshot) setScreenshot(initialScreenshot)
  // eslint-disable-next-line react-hooks/exhaustive-deps -- only a new initialScreenshot from the caller replaces the upload
  }, [initialScreenshot])

  const isDuplicate = !!(user && user.id && hasSubmitted)
  const showNonNumericWarning = isNonNumericScore(score)

  const handleSubmit = async (e) => {
    if (e && e.preventDefault) e.preventDefault()

    const problem = findSubmissionProblem({ isDuplicate, screenshot, score })
    if (problem) {
      showSnackbar(problem, 'error')
      return
    }
    if (isNonNumericScore(score) && !(await confirm(NON_NUMERIC_CONFIRMATION))) return

    await submit(trimScore(score), screenshot)
  }

  const handleCancel = () => {
    onCancel()
    clear()
    setScore('')
  }

  return (
    <Card sx={{ p: 2 }}>
      <Typography variant="h6">Submit</Typography>
      <form onSubmit={handleSubmit}>
        <Stack spacing={2} maxWidth={FORM_MAX_WIDTH} sx={{ mt: 2 }}>
          <TextField label="Score" value={score} onChange={e => setScore(e.target.value)} required />
          {showNonNumericWarning && (
            <Alert severity="warning" sx={{ py: 0 }}>{NON_NUMERIC_SCORE_WARNING}</Alert>
          )}
          <ImageUpload onFileChange={onFileChange} previewUrl={previewUrl} onRemove={clear} />
          <Box sx={{ display: 'flex', gap: 1 }}>
            <AppButton type="submit" disabled={isSubmitting || hasSubmitted}>Submit</AppButton>
            {onCancel && <AppButton color="inherit" variant="outlined" onClick={handleCancel}>Cancel</AppButton>}
          </Box>
        </Stack>
      </form>
      <ConfirmDialog {...dialogProps} />
    </Card>
  )
}
