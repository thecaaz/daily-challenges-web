/**
 * Saves `blob` as `filename` through a temporary link. Never throws: a
 * failure is only logged, the caller handles user-visible errors.
 */
export default function downloadBlob(filename, blob) {
  try {
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.URL.revokeObjectURL(url)
  } catch (err) {
    console.error('downloadBlob failed', err)
  }
}
