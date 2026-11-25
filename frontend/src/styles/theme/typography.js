const HEADING_FONT = "'Baloo 2', 'Poppins', sans-serif"

export default function createTypography(tokens) {
  return {
    fontFamily: "'Poppins', 'Baloo 2', Roboto, Arial, sans-serif",
    h1: {
      fontFamily: HEADING_FONT,
      fontWeight: 800,
      fontSize: '2.2rem',
      letterSpacing: '-0.02em',
    },
    h2: { fontFamily: HEADING_FONT, fontWeight: 700 },
    h3: { fontFamily: HEADING_FONT, fontWeight: 700 },
    h4: { fontFamily: HEADING_FONT, fontWeight: 700 },
    h5: { fontFamily: HEADING_FONT, fontWeight: 700 },
    h6: { fontFamily: HEADING_FONT, fontWeight: 700 },
    button: { fontWeight: 600, textTransform: 'none', letterSpacing: '0.01em' },
    caption: { color: tokens.muted },
  }
}
