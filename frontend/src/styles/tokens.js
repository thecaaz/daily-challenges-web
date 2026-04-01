// JS tokens to mirror the CSS variables in styles/tokens.css
// Provide both light and dark token sets; accept a `mode` parameter.
const light = {
  bg1: '#fff7f0',
  bg2: '#fff0ff',
  accent: '#ff7ab6',
  accent2: '#ffd166',
  muted: '#6b6b6b',
  glass: 'rgba(255,255,255,0.6)',
  radiusCard: '18px',
  radiusBtn: '999px',
  shadowCard: '0 8px 20px rgba(34,34,34,0.08), inset 0 1px 0 rgba(255,255,255,0.6)'
}

const dark = {
  // Dark variant chosen for readable contrast on text and surfaces
  bg1: '#0b1220',
  bg2: '#0f1724',
  accent: '#ff7ab6',
  accent2: '#ffd166',
  muted: '#aeb3b8',
  glass: 'rgba(255,255,255,0.04)',
  radiusCard: '18px',
  radiusBtn: '999px',
  shadowCard: '0 8px 20px rgba(0,0,0,0.6), inset 0 1px 0 rgba(255,255,255,0.02)'
}

export default function tokens(mode = 'light') {
  return mode === 'dark' ? dark : light
}
