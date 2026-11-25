import React from 'react'
import { Button as MuiButton } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'

const NEW_TAB = '_blank'
const SAFE_NEW_TAB_REL = 'noopener noreferrer'

/** Anchor attributes for `href`; a new tab gets a safe `rel` unless one is given. */
function linkProps({ href, target, rel }) {
  if (!href) return {}
  const props = { href }
  if (target) props.target = target
  if (target === NEW_TAB) props.rel = rel || SAFE_NEW_TAB_REL
  else if (rel) props.rel = rel
  return props
}

/**
 * MUI Button that renders as a router Link for `to`, an anchor for `href`, or
 * an explicit `component`. `dataTest` becomes a `data-test` attribute.
 */
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

  const Component = to ? RouterLink : href ? 'a' : component
  const componentProps = { ...(to && { to }), ...linkProps({ href, target, rel }) }

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
