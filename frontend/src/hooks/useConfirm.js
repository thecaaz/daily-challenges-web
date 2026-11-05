import { useState, useCallback, useRef } from 'react'

/**
 * Hook that provides an imperative confirm() replacement using a custom dialog.
 *
 * Usage:
 *   const { confirm, dialogProps } = useConfirm()
 *   const ok = await confirm({ title: '...', message: '...' })
 *   // render <ConfirmDialog {...dialogProps} /> somewhere in your JSX
 *
 * Calling confirm() while a previous dialog is still open resolves the
 * previous promise with `false` so its caller never hangs.
 */
export default function useConfirm() {
  const [state, setState] = useState({ open: false, title: '', message: '', confirmText: 'Delete', confirmColor: 'error' })
  const resolveRef = useRef(null)

  const settle = useCallback((value) => {
    const resolve = resolveRef.current
    resolveRef.current = null
    resolve?.(value)
  }, [])

  const confirm = useCallback(({ title = 'Are you sure?', message = '', confirmText = 'Delete', confirmColor = 'error' } = {}) => {
    settle(false)
    return new Promise((resolve) => {
      resolveRef.current = resolve
      setState({ open: true, title, message, confirmText, confirmColor })
    })
  }, [settle])

  const onConfirm = useCallback(() => {
    settle(true)
    setState(s => ({ ...s, open: false }))
  }, [settle])

  const onCancel = useCallback(() => {
    settle(false)
    setState(s => ({ ...s, open: false }))
  }, [settle])

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
