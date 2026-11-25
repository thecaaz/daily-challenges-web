/**
 * Theme colours that differ between light and dark mode but aren't design
 * tokens. Dark mode uses a solid paper background to avoid layered
 * translucency artifacts.
 */
export default function getModeColors(tokens, isDark) {
  return {
    cardBg: isDark
      ? 'linear-gradient(180deg, rgba(255,255,255,0.02), rgba(255,255,255,0.01))'
      : 'linear-gradient(180deg, rgba(255,255,255,0.92), rgba(255,250,250,0.82))',
    paperBg: isDark ? tokens.bg1 : 'rgba(255,255,255,0.95)',
    textPrimary: isDark ? '#edf2f7' : '#222222',
    appBarBg: isDark ? 'rgba(8,10,12,0.68)' : 'rgba(255,255,255,0.88)',
    appBarBorder: isDark ? '1px solid rgba(255,255,255,0.06)' : '1px solid rgba(0,0,0,0.04)',
    appBarColor: isDark ? '#fff' : '#222',
    inputBorder: isDark ? 'rgba(255,255,255,0.23)' : 'rgba(0,0,0,0.1)',
    tableHeadColor: isDark ? '#d6dde6' : '#444',
  }
}

/** '#ff7ab6' -> '255, 122, 182', for use inside rgba(). */
export function hexToRgb(hex) {
  if (!hex) return '0,0,0'
  const h = hex.replace('#', '')
  const bigint = parseInt(h, 16)
  const r = (bigint >> 16) & 255
  const g = (bigint >> 8) & 255
  const b = bigint & 255
  return `${r}, ${g}, ${b}`
}
