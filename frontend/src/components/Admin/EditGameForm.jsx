import React from 'react'
import { TextField, Stack, Paper, Typography } from '@mui/material'
import AppButton from '../ui/AppButton'
import ImageUpload from '../ui/ImageUpload/ImageUpload'
import ResetTimeInput from './ResetTimeInput'
import RankingModeSelect from './RankingModeSelect'

/** Form for the game being edited. `editor` is the result of `useGameEditor`. */
export default function EditGameForm({ editor }) {
  const { editingGame: game, setField, save, cancel, image } = editor

  return (
    <Paper sx={{ p: 1.5, mt: 1.5 }}>
      <Typography variant="h6" sx={{ mb: 1 }}>Edit Game</Typography>
      <Stack spacing={2} maxWidth={600}>
        <TextField label="Name" value={game.name ?? ''} onChange={e => setField('name', e.target.value)} />
        <TextField label="URL" value={game.url ?? ''} onChange={e => setField('url', e.target.value)} type="url" helperText="Leave empty to remove the URL" />
        <TextField label="Description" value={game.description ?? ''} onChange={e => setField('description', e.target.value)} multiline rows={3} />
        <ResetTimeInput
          label="Reset time (your local time)"
          value={game.localResetTime}
          onChange={value => setField('localResetTime', value)}
        />
        <RankingModeSelect value={game.rankingMode ?? 'highest'} onChange={value => setField('rankingMode', value)} />
        <div>
          <ImageUpload onFileChange={image.onFileChange} previewUrl={image.previewUrl} onRemove={image.clear} />
        </div>
        <div>
          <AppButton onClick={save}>Save</AppButton>
          <AppButton onClick={cancel}>Cancel</AppButton>
        </div>
      </Stack>
    </Paper>
  )
}
