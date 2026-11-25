import { utcToLocalHHmm } from './resetTime'

/**
 * The API stores and returns the reset time as UTC "HH:mm"; admin forms show
 * the admin's local time. Falls back to midnight for a missing or invalid value.
 */
export default function toLocalResetTime(utcHHmm) {
  return utcToLocalHHmm(utcHHmm) ?? '00:00'
}
