import React from 'react'
import { Button as MuiButton } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'

const AppButton = React.forwardRef(function AppButton(props, ref) {
  const {
    to,
    href,
    target,
    rel,
    dataTest,
    children,
    startIcon,
    endIcon,
    variant = 'contained',
    color = 'primary',
    size = 'medium',
    component,
    ...rest
  } = props

  // Choose underlying component: Router Link for `to`, anchor for `href`, or allow explicit `component`.
  const Component = to ? RouterLink : href ? 'a' : component

  const componentProps = {}
  if (to) componentProps.to = to
  if (href) componentProps.href = href
  if (href && target) componentProps.target = target

  // For external links opened in a new tab, default to safer rel values if none provided
  if (href && target === '_blank') {
    componentProps.rel = rel || 'noopener noreferrer'
  } else if (href && rel) {
    componentProps.rel = rel
  }

  return (
    <MuiButton
      ref={ref}
      component={Component}
      startIcon={startIcon}
      endIcon={endIcon}
      variant={variant}
      color={color}
      size={size}
      data-test={dataTest}
      {...componentProps}
      {...rest}
    >
      {children}
    </MuiButton>
  )
})

export default AppButton
