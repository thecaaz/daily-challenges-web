import { useState } from 'react'
import api from '../api'
import { useSnackbar } from '../contexts/SnackbarContext'
import { getApiErrorMessage } from '../utils/apiError'
import formatNumber from '../utils/formatNumber'

/** Parse an XP amount field: a non-zero whole number, or null. */
export function parseXpAmount(value) {
  const text = String(value ?? '').trim()
  if (!/^[+-]?\d+$/.test(text)) return null
  const amount = Number(text)
  if (!Number.isSafeInteger(amount) || amount === 0) return null
  return Math.abs(amount)
}

/**
 * Per-user XP amount fields and the Add / Deduct actions of the admin user list.
 *
 * Applying asks for an optional reason first (cancelling the prompt does
 * nothing), then clears the user's field and calls `onAdjusted`.
 *
 * @param {object} options
 * @param {(dialog: object) => Promise<string|null>} options.prompt - from usePrompt.
 * @param {() => void} options.onAdjusted
 */
export default function useXpAdjustment({ prompt, onAdjusted }) {
  const { showSnackbar } = useSnackbar()
  const [amountByUserId, setAmountByUserId] = useState({})

  const amountFor = userId => amountByUserId[userId] ?? ''
  const setAmount = (userId, amount) => setAmountByUserId(prev => ({ ...prev, [userId]: amount }))
  const isValid = userId => parseXpAmount(amountByUserId[userId]) != null
  const isInvalidInput = userId => amountFor(userId) !== '' && !isValid(userId)

  const applyDelta = async (userId, delta) => {
    const verb = delta >= 0 ? 'Adding' : 'Deducting'
    const reason = await prompt({
      title: 'XP Adjustment',
      message: `${verb} ${formatNumber(Math.abs(delta))} XP`,
      label: 'Reason (optional)',
      confirmText: 'Apply',
    })
    if (reason === null) return
    try {
      await api.post(`/admin/users/${userId}/xp`, { delta, reason })
      showSnackbar('XP adjusted', 'success')
      setAmount(userId, '')
      onAdjusted()
    } catch (err) {
      showSnackbar(getApiErrorMessage(err, 'Failed to adjust XP'), 'error')
    }
  }

  const adjustBy = (userId, toDelta) => {
    const amount = parseXpAmount(amountByUserId[userId])
    if (amount == null) {
      showSnackbar('Enter a whole number of XP (not 0)', 'error')
      return
    }
    applyDelta(userId, toDelta(amount))
  }

  return {
    amountFor,
    setAmount,
    isValid,
    isInvalidInput,
    add: userId => adjustBy(userId, amount => amount),
    deduct: userId => adjustBy(userId, amount => -amount),
  }
}
