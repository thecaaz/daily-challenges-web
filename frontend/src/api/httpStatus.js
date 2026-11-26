/** HTTP status checks for axios errors. */
const UNAUTHORIZED = 401
const NOT_FOUND = 404

export const isUnauthorizedError = error => error?.response?.status === UNAUTHORIZED

export const isNotFoundError = error => error?.response?.status === NOT_FOUND
