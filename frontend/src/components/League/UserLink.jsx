import React from 'react'
import { Link } from 'react-router-dom'

const plainLinkStyle = { textDecoration: 'none', color: 'inherit' }

/** Link to a user's profile that looks like plain text. */
export default function UserLink({ userId, children }) {
  return (
    <Link to={`/users/${userId}`} style={plainLinkStyle}>
      {children}
    </Link>
  )
}
