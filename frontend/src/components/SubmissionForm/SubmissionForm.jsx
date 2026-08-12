import React, { useEffect, useState } from 'react'
import { Stack, TextField, Alert, Box, Card, Typography } from '@mui/material'
import AppButton from '../ui/AppButton'
import ImageUpload from '../ui/ImageUpload/ImageUpload'
import useImageUpload from '../../hooks/useImageUpload'
import parseScore from '../../utils/parseScore'
import api from '../../api'
import { useSnackbar } from '../../contexts/SnackbarContext'
import useRequireAuth from '../../hooks/useRequireAuth'
import useConfirm from '../../hooks/useConfirm'
import ConfirmDialog from '../ui/ConfirmDialog'
import parseUtcDate from '../../utils/parseUtcDate'
import { useNavigate } from 'react-router-dom'

export default function SubmissionForm({ gameId, initialScore = '', initialScreenshot = null, hasSubmitted = false, onCancel, onSubmitted }) {
  const navigate = useNavigate()
  const { showSnackbar } = useSnackbar()
  const { user, fetchMe } = useRequireAuth()
  const { confirm, dialogProps } = useConfirm()

  const [score, setScore] = useState(initialScore)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const { screenshot, previewUrl, setScreenshot, onFileChange, clear } = useImageUpload(showSnackbar)

  useEffect(() => {
    if (initialScore !== undefined && initialScore !== score) setScore(initialScore)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialScore])

  useEffect(() => {
    if (initialScreenshot) setScreenshot(initialScreenshot)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialScreenshot])

  const submit = async (e) => {
    if (e && e.preventDefault) e.preventDefault()

    if (user && user.id && hasSubmitted) {
      showSnackbar('You have already submitted for this game', 'error')
      return
    }

    // Client-side enforcement: require a screenshot
    if (!screenshot) {
      showSnackbar('Screenshot is required', 'error')
      return
    }

    // Basic client-side validation for type and size
    const allowedTypes = ['image/png', 'image/jpeg', 'image/webp']
    if (screenshot.type && !allowedTypes.includes(screenshot.type)) {
      showSnackbar('Allowed formats: PNG, JPEG, WebP', 'error')
      return
    }
    if (screenshot.size && screenshot.size > 2 * 1024 * 1024) {
      showSnackbar('Screenshot must be 2 MB or smaller', 'error')
      return
    }

    const fd = new FormData()
    fd.append('gameId', gameId)
    const parsed = parseScore(score)
    if (isNaN(parsed)) {
      const ok = await confirm({ title: 'Non-numeric score', message: 'This score is not a number and may not show up correctly on leaderboards. Submit anyway?', confirmText: 'Submit', confirmColor: 'primary' })
      if (!ok) return
    }
    // Send the locale-normalised value so the server stores a canonical number.
    // A German player typing "40.456" means 40456, not 40.456.
    fd.append('score', isNaN(parsed) ? score : String(parsed))
    fd.append('screenshot', screenshot)

    setIsSubmitting(true)
    try {
      const res = await api.post('/submissions', fd)
      const { submission, xpGain } = res.data

      const prevLevel = user?.level ?? 1
      const updatedUser = await fetchMe()
      const leveledUp = updatedUser && updatedUser.level > prevLevel

      if (leveledUp) {
        await fetchMe()
        showSnackbar(xpGain > 0 ? `Submitted! +${xpGain} XP ⬆️ Level ${updatedUser.level}!` : `Level ${updatedUser.level}!`, 'success')
      } else if (xpGain > 0) {
        showSnackbar(`Submitted! +${xpGain} XP`, 'success')
      } else {
        showSnackbar('Submitted!', 'success')
      }

      const date = submission?.createdAt
        ? parseUtcDate(submission.createdAt).toISOString().split('T')[0]
        : new Date().toISOString().split('T')[0]

      // If a caller provided an onSubmitted handler, call it instead of navigating
      if (typeof onSubmitted === 'function') {
        try {
          await fetchMe()
        } catch (e) {
          // ignore
        }
        onSubmitted({ submission, xpGain })
        return
      }

      navigate(`/games/${gameId}`)
    } catch (err) {
      // Treat server-side duplicate-submission (409) as success when caller wants to handle it
      const status = err?.response?.status
      if (status === 409 && typeof onSubmitted === 'function') {
        try {
          await fetchMe()
        } catch (e) {}
        onSubmitted({ submission: err?.response?.data?.submission || null, xpGain: err?.response?.data?.xpGain || 0, duplicate: true })
        return
      }

      const msg = err?.response?.data?.message || err?.response?.data || err?.message || 'Failed to submit'
      showSnackbar(String(msg), 'error')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Card sx={{ p: 2 }}>
      <Typography variant="h6">Submit</Typography>
      <form onSubmit={submit}>
        <Stack spacing={2} maxWidth={480} sx={{ mt: 2 }}>
          <TextField label="Score" value={score} onChange={e => setScore(e.target.value)} required />
          {score !== '' && isNaN(parseScore(score)) && (
            <Alert severity="warning" sx={{ py: 0 }}>This score is not a number and may not show up correctly on leaderboards.</Alert>
          )}
          <ImageUpload onFileChange={onFileChange} previewUrl={previewUrl} onRemove={clear} />
          <Box sx={{ display: 'flex', gap: 1 }}>
            <AppButton type="submit" disabled={isSubmitting || hasSubmitted}>Submit</AppButton>
            {onCancel && <AppButton color="inherit" variant="outlined" onClick={() => { onCancel(); clear(); setScore('') }}>Cancel</AppButton>}
          </Box>
        </Stack>
      </form>
      <ConfirmDialog {...dialogProps} />
    </Card>
  )
}
