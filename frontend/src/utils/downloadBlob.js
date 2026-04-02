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
    // silent fail — caller should handle user-visible errors
    console.error('downloadBlob failed', err)
  }
}
