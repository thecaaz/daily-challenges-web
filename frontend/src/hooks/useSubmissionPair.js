import api from '../api'
import useAsyncData from './useAsyncData'

const getSubmission = id => api.get(`/submissions/${id}`).then(response => response.data)

/**
 * Two submissions to compare, loaded together: if either fails, `error` is set
 * and both are null.
 *
 * @returns {{ first: object|null, second: object|null, loading: boolean, error: boolean }}
 */
export default function useSubmissionPair(firstId, secondId) {
  const { data, loading, error } = useAsyncData(
    () => Promise.all([getSubmission(firstId), getSubmission(secondId)]),
    [firstId, secondId]
  )
  const [first = null, second = null] = data ?? []
  return { first, second, loading, error }
}
