import React from 'react'
import UserLink from './UserLink'

/** Wraps `children` in a profile link when there is a `userId`; renders them unlinked otherwise. */
export default function OptionalUserLink({ userId, children }) {
  if (!userId) return children
  return <UserLink userId={userId}>{children}</UserLink>
}
