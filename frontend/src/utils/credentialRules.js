export const USERNAME_MIN = 3
export const USERNAME_MAX = 32
export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 128
const USERNAME_PATTERN = /^[A-Za-z0-9_.-]+$/

export const PASSWORD_LENGTH_MESSAGE = `Password must be ${PASSWORD_MIN}–${PASSWORD_MAX} characters`

export const isValidPasswordLength = password => password.length >= PASSWORD_MIN && password.length <= PASSWORD_MAX

/**
 * Login only requires non-empty fields, so accounts that predate the
 * new-account rules can still sign in. Returns { username?, password? } messages.
 */
export function requireCredentials(username, password) {
  const errors = {}
  if (!username) errors.username = 'Enter your username'
  if (!password) errors.password = 'Enter your password'
  return errors
}

/** Server rules for a new account (Register). Returns { username?, password? } messages. */
export function validateNewCredentials(username, password) {
  const errors = {}
  const trimmedUsername = username.trim()
  if (trimmedUsername.length < USERNAME_MIN || trimmedUsername.length > USERNAME_MAX) {
    errors.username = `Username must be ${USERNAME_MIN}–${USERNAME_MAX} characters`
  } else if (!USERNAME_PATTERN.test(trimmedUsername)) {
    errors.username = 'Use only letters, digits, "_", "." and "-"'
  }
  if (!isValidPasswordLength(password)) errors.password = PASSWORD_LENGTH_MESSAGE
  return errors
}

/** Field setup for signing in to an existing account. */
export const LOGIN_CREDENTIALS = {
  validate: requireCredentials,
  username: {},
  password: { autoComplete: 'current-password' },
}

/** Field setup for creating an account: hints and limits mirror validateNewCredentials. */
export const NEW_ACCOUNT_CREDENTIALS = {
  validate: validateNewCredentials,
  username: {
    hint: `${USERNAME_MIN}–${USERNAME_MAX} characters: letters, digits, _ . -`,
    inputProps: { maxLength: USERNAME_MAX },
  },
  password: {
    autoComplete: 'new-password',
    hint: `${PASSWORD_MIN}–${PASSWORD_MAX} characters`,
    inputProps: { maxLength: PASSWORD_MAX },
  },
}
