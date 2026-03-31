import { useState, useCallback, useRef } from 'react'

/**
 * Hook that provides an imperative prompt() replacement using a custom dialog.
 *
 * Usage:
 *   const { prompt, dialogProps } = usePrompt()
 *   const value = await prompt({ title: '...', label: '...' })
 *   // value is the string entered, or null if cancelled
 *   // render <PromptDialog {...dialogProps} /> somewhere in your JSX
 */
export default function usePrompt() {
  const [state, setState] = useState({ open: false, title: '', message: '', label: '', confirmText: 'Confirm', value: '' })
  const resolveRef = useRef(null)

  const prompt = useCallback(({ title = '', message = '', label = '', confirmText = 'Confirm' } = {}) => {
    return new Promise((resolve) => {
      resolveRef.current = resolve
      setState({ open: true, title, message, label, confirmText, value: '' })
    })
  }, [])

  const onChange = useCallback((v) => {
    setState(s => ({ ...s, value: v }))
  }, [])

  const onConfirm = useCallback(() => {
    resolveRef.current?.(state.value)
    resolveRef.current = null
    setState(s => ({ ...s, open: false }))
  }, [state.value])

  const onCancel = useCallback(() => {
    resolveRef.current?.(null)
    resolveRef.current = null
    setState(s => ({ ...s, open: false }))
  }, [])

  const dialogProps = {
    open: state.open,
    title: state.title,
    message: state.message,
    label: state.label,
    confirmText: state.confirmText,
    value: state.value,
    onChange,
    onConfirm,
    onCancel,
  }

  return { prompt, dialogProps }
}
