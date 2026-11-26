/**
 * Saves `blob` as `filename` through a temporary link. Never throws: a
 * failure is only logged, the caller handles user-visible errors.
 */
export default function downloadBlob(filename, blob) {
  try {
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  } catch (err) {
    console.error('downloadBlob failed', err)
  }
}
