import api from '../api'
import useAsyncData from './useAsyncData'
import { isNotFoundError } from '../api/httpStatus'

/** The submission, or null when it does not exist (404); other failures reject. */
function getSubmissionOrNull(id) {
  return api.get(`/submissions/${id}`)
    .then(response => response.data ?? null)
    .catch(err => {
      if (isNotFoundError(err)) return null
      throw err
    })
}

/**
 * One submission by id. `submission` is null while loading, on an error and
 * when it does not exist; `error` is only set for failures other than a 404.
 *
 * @returns {{ submission: object|null, loading: boolean, error: boolean, retry: () => void }}
 */
export default function useSubmission(id) {
  const { data, loading, error, refetch } = useAsyncData(() => getSubmissionOrNull(id), [id])
  return { submission: data, loading, error, retry: refetch }
}
