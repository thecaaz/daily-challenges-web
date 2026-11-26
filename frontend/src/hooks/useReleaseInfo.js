import api from '../api'
import useAsyncData from './useAsyncData'

/**
 * A failed lookup is not reported: the version badge is decoration, so it is
 * simply left out.
 */
const NO_RELEASE_INFO = null

/**
 * The running release (`{ version, rawVersion?, changelog? }`) from `/info`,
 * or `null` while loading or when it cannot be loaded.
 */
export default function useReleaseInfo() {
  const { data, error } = useAsyncData(() => api.get('/info').then(response => response.data), [])
  return error ? NO_RELEASE_INFO : data
}
