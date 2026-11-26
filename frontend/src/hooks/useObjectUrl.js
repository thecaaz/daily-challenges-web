import { useEffect, useState } from 'react'

/**
 * A blob: URL for displaying `file`, revoked when the file changes or the
 * component unmounts. Null without a file.
 *
 * @param {Blob|null} file
 * @returns {string|null}
 */
export default function useObjectUrl(file) {
  const [objectUrl, setObjectUrl] = useState(null)

  useEffect(() => {
    if (!file) {
      setObjectUrl(null)
      return undefined
    }
    const url = URL.createObjectURL(file)
    setObjectUrl(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  return objectUrl
}
