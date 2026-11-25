import { useState } from 'react'
import { useNavigate } from 'react-router-dom'

const COMPARED_COUNT = 2

function toggleSubmission(selection, submission) {
  if (selection.some(s => s.id === submission.id)) return selection.filter(s => s.id !== submission.id)
  if (selection.length >= COMPARED_COUNT) return [selection[1], submission]
  return [...selection, submission]
}

/**
 * Compare mode for a submissions list: pick two submissions and open them
 * side by side. Selecting a third drops the oldest pick.
 * The selection belongs to one list: it is empty again once `listKey` changes.
 *
 * @param {string} listKey - identifies the list the submissions come from.
 */
export default function useCompareSelection(listKey) {
  const navigate = useNavigate()
  const [isComparing, setIsComparing] = useState(false)
  const [picked, setPicked] = useState({ listKey, submissions: [] })
  const selection = picked.listKey === listKey ? picked.submissions : []
  const canCompare = isComparing && selection.length === COMPARED_COUNT

  const toggleMode = () => {
    setIsComparing(prev => !prev)
    setPicked({ listKey, submissions: [] })
  }

  const toggle = (submission) => {
    setPicked(prev => ({
      listKey,
      submissions: toggleSubmission(prev.listKey === listKey ? prev.submissions : [], submission),
    }))
  }

  const isSelected = (submissionId) => selection.some(s => s.id === submissionId)

  const compare = () => {
    if (selection.length === COMPARED_COUNT) navigate(`/compare/${selection[0].id}/${selection[1].id}`)
  }

  return { isComparing, canCompare, toggleMode, toggle, isSelected, compare }
}
