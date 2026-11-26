export default function createPalette(mode, tokens, colors) {
  return {
    mode,
    primary: {
      main: tokens.accent,
      light: '#ffaad4',
      dark: '#e05090',
      contrastText: '#ffffff',
    },
    secondary: {
      main: tokens.accent2,
      light: '#ffe299',
      dark: '#c9a23d',
      contrastText: '#222222',
    },
    background: {
      default: tokens.bg1,
      paper: colors.paperBg,
    },
    text: {
      primary: colors.textPrimary,
      secondary: tokens.muted,
    },
    error: { main: '#f44336' },
    success: { main: '#66bb6a' },
  }
}
