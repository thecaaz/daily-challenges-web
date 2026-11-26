// Design tokens: the single source for colours, radii and shadows.
// The MUI theme reads them directly; plain CSS (styles.css) reads them as CSS
// custom properties, injected by ThemeContext via MUI <GlobalStyles>.
const light = {
  bg1: '#fff7f0',
  bg2: '#fff0ff',
  accent: '#ff7ab6',
  accent2: '#ffd166',
  muted: '#6b6b6b',
  glass: 'rgba(255,255,255,0.6)',
  codeBackground: 'rgba(0,0,0,0.04)',
  radiusCard: '18px',
  radiusBtn: '999px',
  shadowCard: '0 8px 20px rgba(34,34,34,0.08), inset 0 1px 0 rgba(255,255,255,0.6)'
}

/** Dark variant chosen for readable contrast on text and surfaces. */
const dark = {
  bg1: '#0b1220',
  bg2: '#0f1724',
  accent: '#ff7ab6',
  accent2: '#ffd166',
  muted: '#aeb3b8',
  glass: 'rgba(255,255,255,0.04)',
  codeBackground: 'rgba(255,255,255,0.04)',
  radiusCard: '18px',
  radiusBtn: '999px',
  shadowCard: '0 8px 20px rgba(0,0,0,0.6), inset 0 1px 0 rgba(255,255,255,0.02)'
}

/** Trophy colour of a scoring-day winner; the same in both modes. */
export const winnerGold = '#FFD700'

/** Medal colours of the higher achievement tiers; the same in both modes. */
export const medalColors = { gold: winnerGold, silver: '#C0C0C0' }

/**
 * Inline warning text (e.g. a score that is not a number). MUI's light-mode
 * warning.main, kept in dark mode too: the theme has no warning colour of its own.
 */
export const warningOrange = '#ed6c02'

/** Hover label and cursor of the small Recharts charts; the same in both modes. */
export const chartTooltip = {
  background: 'rgba(0,0,0,0.75)',
  text: '#fff',
  cursorFill: 'rgba(255,255,255,0.05)',
}

/** Text over the darkened game-header image; the same in both modes. */
export const onImageText = {
  body: 'rgba(255,255,255,0.9)',
  caption: 'rgba(255,255,255,0.7)',
}

/** Hairline outline of the version chip in the app bar; the same in both modes. */
export const faintOutline = '1px solid rgba(255,255,255,0.08)'

/** Neutral frame around screenshots and image previews; the same in both modes. */
export const imageFrame = {
  borderColor: '#ddd',
  background: '#fafafa',
}

/** The app icon tile in the header; the same in both modes. */
export const brandIcon = {
  background: 'linear-gradient(180deg,#fff,#ffeef8)',
  shadow: '0 4px 12px rgba(255,122,182,0.15)',
}

/**
 * Diagonal accent-to-accent2 (pink to yellow) wash behind a game without an
 * image, at the given opacity (0–1); the same in both modes.
 */
export function placeholderGradient(opacity) {
  return `linear-gradient(135deg, rgba(255,122,182,${opacity}), rgba(255,209,102,${opacity}))`
}

export default function tokens(mode = 'light') {
  return mode === 'dark' ? dark : light
}

const CSS_VAR_NAMES = {
  bg1: '--bg-1',
  bg2: '--bg-2',
  accent: '--accent',
  accent2: '--accent-2',
  muted: '--muted',
  glass: '--glass',
  radiusCard: '--radius-card',
  radiusBtn: '--radius-btn',
  shadowCard: '--shadow-card',
}

/** CSS custom properties (e.g. { '--bg-1': '#fff7f0' }) for a mode. */
export function cssVariables(mode = 'light') {
  const t = tokens(mode)
  const vars = {}
  for (const [key, name] of Object.entries(CSS_VAR_NAMES)) vars[name] = t[key]
  return vars
}
