import React from 'react'
import { TextField, Stack } from '@mui/material'
import AppButton from '../ui/AppButton'
import ImageUpload from '../ui/ImageUpload/ImageUpload'
import useCreateGameForm from '../../hooks/useCreateGameForm'
import ResetTimeInput from './ResetTimeInput'
import RankingModeSelect from './RankingModeSelect'

/** Form for adding a new game. `acceptsPaste` routes pasted images to this form. */
export default function CreateGameForm({ acceptsPaste }) {
  const { fields, setField, submit, image } = useCreateGameForm({ acceptsPaste })

  return (
    <form onSubmit={submit}>
      <Stack spacing={2} maxWidth={400}>
        <TextField label="Game name" value={fields.name} onChange={e => setField('name', e.target.value)} required />
        <TextField label="Game URL (optional)" value={fields.url} onChange={e => setField('url', e.target.value)} type="url" helperText="Absolute http(s) URL" />
        <TextField label="Description" value={fields.description} onChange={e => setField('description', e.target.value)} multiline rows={3} />
        <ImageUpload onFileChange={image.onFileChange} previewUrl={image.previewUrl} onRemove={image.clear} />
        <ResetTimeInput
          label="Reset time (day boundary, your local time)"
          value={fields.localResetTime}
          onChange={value => setField('localResetTime', value)}
        />
        <RankingModeSelect value={fields.rankingMode} onChange={value => setField('rankingMode', value)} />
        <AppButton type="submit">Create Game</AppButton>
      </Stack>
    </form>
  )
}
