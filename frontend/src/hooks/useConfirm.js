import { useState, useCallback } from 'react'

/**
 * Hook that provides an imperative confirm() replacement using a custom dialog.
 *
 * Usage:
 *   const { confirm, dialogProps } = useConfirm()
 *   const ok = await confirm({ title: '...', message: '...' })
 *   // render <ConfirmDialog {...dialogProps} /> somewhere in your JSX
 */
export default function useConfirm() {
  const [state, setState] = useState({ open: false, title: '', message: '', confirmText: 'Delete', confirmColor: 'error', resolve: null })

  const confirm = useCallback(({ title = 'Are you sure?', message = '', confirmText = 'Delete', confirmColor = 'error' } = {}) => {
    return new Promise((resolve) => {
      setState({ open: true, title, message, confirmText, confirmColor, resolve })
    })
  }, [])

  const onConfirm = useCallback(() => {
    state.resolve?.(true)
    setState(s => ({ ...s, open: false }))
  }, [state.resolve])

  const onCancel = useCallback(() => {
    state.resolve?.(false)
    setState(s => ({ ...s, open: false }))
  }, [state.resolve])

  const dialogProps = {
    open: state.open,
    title: state.title,
    message: state.message,
    confirmText: state.confirmText,
    confirmColor: state.confirmColor,
    onConfirm,
    onCancel,
  }

  return { confirm, dialogProps }
}
