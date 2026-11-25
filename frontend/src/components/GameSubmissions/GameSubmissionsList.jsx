import React from 'react'
import { Box, Typography, CircularProgress } from '@mui/material'
import AppButton from '../ui/AppButton'
import SubmissionCard from '../SubmissionCard'
import SubmissionGrid from '../ui/SubmissionGrid/SubmissionGrid'

/**
 * A page-by-page list of submissions with its error, empty and loading states.
 * `list` is the result of `useSubmissionPages`, `compare` of `useCompareSelection`.
 */
export default function GameSubmissionsList({ list, compare }) {
  const isEmpty = !list.hasError && !list.isLoading && list.submissions.length === 0
  const isLoadingFirstPage = list.isLoading && list.submissions.length === 0

  return (
    <>
      {list.hasError && <ListLoadError onRetry={list.retry} />}
      {isEmpty && (
        <Typography color="text.secondary" sx={{ mt: 2 }}>No submissions for this day yet.</Typography>
      )}
      <SubmissionGrid
        items={list.submissions}
        ItemComponent={SubmissionCard}
        containerSx={{ mt: 1 }}
        itemProps={(item) => ({
          compareMode: compare.isComparing,
          selected: compare.isSelected(item.id),
          onToggleCompare: compare.toggle,
        })}
      />
      {isLoadingFirstPage && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress size={28} /></Box>
      )}
      {list.hasMore && <LoadMoreButton isLoading={list.isLoading} onClick={list.loadMore} />}
    </>
  )
}

function ListLoadError({ onRetry }) {
  return (
    <Box sx={{ textAlign: 'center', py: 3 }}>
      <Typography gutterBottom>Failed to load submissions.</Typography>
      <AppButton onClick={onRetry}>Retry</AppButton>
    </Box>
  )
}

function LoadMoreButton({ isLoading, onClick }) {
  return (
    <div style={{ marginTop: 16, textAlign: 'center' }}>
      <AppButton onClick={onClick} disabled={isLoading}>
        {isLoading ? 'Loading…' : 'Load more'}
      </AppButton>
    </div>
  )
}
