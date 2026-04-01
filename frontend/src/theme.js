import { createTheme } from '@mui/material/styles'
import getTokens from './styles/tokens'

export default function createAppTheme(mode = 'light') {
  const tokens = getTokens(mode)
  const isDark = mode === 'dark'

  function hexToRgb(hex) {
    if (!hex) return '0,0,0'
    const h = hex.replace('#', '')
    const bigint = parseInt(h, 16)
    const r = (bigint >> 16) & 255
    const g = (bigint >> 8) & 255
    const b = bigint & 255
    return `${r}, ${g}, ${b}`
  }

  const cardBg = isDark
    ? 'linear-gradient(180deg, rgba(255,255,255,0.02), rgba(255,255,255,0.01))'
    : 'linear-gradient(180deg, rgba(255,255,255,0.92), rgba(255,250,250,0.82))'

  // Use solid paper background for dark mode to avoid layered translucency artifacts
  const paperBg = isDark ? tokens.bg1 : 'rgba(255,255,255,0.95)'
  const textPrimary = isDark ? '#edf2f7' : '#222222'
  const appBarBg = isDark ? 'rgba(8,10,12,0.68)' : 'rgba(255,255,255,0.88)'
  const appBarBorder = isDark ? '1px solid rgba(255,255,255,0.06)' : '1px solid rgba(0,0,0,0.04)'
  const appBarColor = isDark ? '#fff' : '#222'

  return createTheme({
    palette: {
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
        paper: paperBg,
      },
      text: {
        primary: textPrimary,
        secondary: tokens.muted,
      },
      error: { main: '#f44336' },
      success: { main: '#66bb6a' },
    },

    typography: {
      fontFamily: "'Poppins', 'Baloo 2', Roboto, Arial, sans-serif",
      h1: {
        fontFamily: "'Baloo 2', 'Poppins', sans-serif",
        fontWeight: 800,
        fontSize: '2.2rem',
        letterSpacing: '-0.02em',
      },
      h2: { fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 700 },
      h3: { fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 700 },
      h4: { fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 700 },
      h5: { fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 700 },
      h6: { fontFamily: "'Baloo 2', 'Poppins', sans-serif", fontWeight: 700 },
      button: { fontWeight: 600, textTransform: 'none', letterSpacing: '0.01em' },
      caption: { color: '#6b6b6b' },
    },

    shape: {
      borderRadius: 14,
    },

    shadows: [
      'none',
      '0 2px 6px rgba(34,34,34,0.06)',
      '0 4px 10px rgba(34,34,34,0.08)',
      '0 6px 14px rgba(34,34,34,0.09)',
      '0 8px 20px rgba(34,34,34,0.1)',
      '0 10px 24px rgba(34,34,34,0.11)',
      '0 12px 28px rgba(34,34,34,0.12)',
      '0 14px 32px rgba(34,34,34,0.12)',
      '0 16px 36px rgba(34,34,34,0.13)',
      '0 18px 40px rgba(34,34,34,0.13)',
      '0 20px 44px rgba(34,34,34,0.14)',
      '0 22px 48px rgba(34,34,34,0.14)',
      '0 24px 52px rgba(34,34,34,0.15)',
      '0 26px 56px rgba(34,34,34,0.15)',
      '0 28px 60px rgba(34,34,34,0.16)',
      '0 30px 64px rgba(34,34,34,0.16)',
      '0 32px 68px rgba(34,34,34,0.17)',
      '0 34px 72px rgba(34,34,34,0.17)',
      '0 36px 76px rgba(34,34,34,0.18)',
      '0 38px 80px rgba(34,34,34,0.18)',
      '0 40px 84px rgba(34,34,34,0.19)',
      '0 42px 88px rgba(34,34,34,0.19)',
      '0 44px 92px rgba(34,34,34,0.20)',
      '0 46px 96px rgba(34,34,34,0.20)',
      '0 48px 100px rgba(34,34,34,0.21)',
    ],

    components: {
      /* ── Buttons ── */
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: {
            borderRadius: 999,
            padding: '9px 22px',
            transition: 'transform 150ms ease, box-shadow 150ms ease',
            '&:hover': {
              transform: 'translateY(-2px)',
            },
            '&:active': {
              transform: 'translateY(1px) scale(0.997)',
            },
          },
          containedPrimary: {
            background: `linear-gradient(90deg, ${tokens.accent}, ${tokens.accent2})`,
            color: '#fff',
            boxShadow: '0 6px 18px rgba(255,122,182,0.22)',
            '&:hover': {
              background: 'linear-gradient(90deg, #ff90c4, #ffe080)',
              boxShadow: '0 10px 24px rgba(255,122,182,0.30)',
            },
            textShadow: '0 1px 0 rgba(0,0,0,0.18)',
            '&.Mui-disabled': {
              // Keep readable contrast while indicating disabled state
              background: `linear-gradient(90deg, rgba(${hexToRgb(tokens.accent)},0.36), rgba(${hexToRgb(tokens.accent2)},0.36))`,
              color: 'rgba(255,255,255,0.95)',
              boxShadow: 'none',
              textShadow: 'none',
            },
          },
          containedSecondary: {
            background: tokens.accent2,
            color: '#222',
            boxShadow: '0 6px 18px rgba(255,209,102,0.3)',
            '&:hover': {
              background: '#ffe080',
              boxShadow: '0 10px 24px rgba(255,209,102,0.4)',
            },
            textShadow: '0 1px 0 rgba(0,0,0,0.08)',
            '&.Mui-disabled': {
              background: `linear-gradient(90deg, rgba(${hexToRgb(tokens.accent2)},0.36), rgba(${hexToRgb(tokens.accent)},0.18))`,
              color: isDark ? 'rgba(0,0,0,0.85)' : 'rgba(34,34,34,0.9)',
              boxShadow: 'none',
              textShadow: 'none',
            },
          },
          outlinedPrimary: {
            borderColor: tokens.accent,
            color: tokens.accent,
            '&:hover': {
              background: 'rgba(255,122,182,0.06)',
              borderColor: '#e05090',
            },
          },
          text: {
            '&:hover': {
              background: 'rgba(255,122,182,0.06)',
            },
          },
        },
      },

      /* ── Cards ── */
      MuiCard: {
        styleOverrides: {
          root: {
            background: cardBg,
            borderRadius: 18,
            boxShadow: tokens.shadowCard,
            border: isDark ? '1px solid rgba(255,255,255,0.02)' : '1px solid rgba(0,0,0,0.04)',
            transition: 'transform 180ms cubic-bezier(.2,.9,.2,1), box-shadow 180ms',
            '&:hover': {
              transform: 'translateY(-6px)',
              boxShadow: isDark ? '0 16px 32px rgba(0,0,0,0.6)' : '0 16px 32px rgba(34,34,34,0.13)',
            },
          },
        },
      },

      MuiCardActionArea: {
        styleOverrides: {
          root: {
            borderRadius: 18,
            '&:hover .MuiCardActionArea-focusHighlight': {
              opacity: 0,
            },
          },
        },
      },

      /* ── Paper ── */
      MuiPaper: {
        styleOverrides: {
          root: {
            borderRadius: 18,
            background: paperBg,
            boxShadow: isDark ? '0 8px 24px rgba(0,0,0,0.6)' : '0 8px 24px rgba(34,34,34,0.08)',
          },
          elevation0: { boxShadow: 'none' },
        },
      },

      /* ── Text fields ── */
      MuiTextField: {
        defaultProps: { variant: 'outlined', fullWidth: true },
        styleOverrides: {
          root: {
            '& .MuiOutlinedInput-root': {
              borderRadius: 12,
              background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(255,255,255,0.9)',
              '& fieldset': { borderColor: 'rgba(0,0,0,0.1)' },
              '&:hover fieldset': { borderColor: tokens.accent },
              '&.Mui-focused fieldset': { borderColor: tokens.accent, borderWidth: 2 },
            },
            '& .MuiInputLabel-root.Mui-focused': { color: tokens.accent },
          },
        },
      },

      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: 12,
            background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(255,255,255,0.9)',
            '& fieldset': { borderColor: 'rgba(0,0,0,0.1)' },
            '&:hover fieldset': { borderColor: tokens.accent },
            '&.Mui-focused fieldset': { borderColor: tokens.accent, borderWidth: 2 },
          },
        },
      },

      /* ── Chip ── */
      MuiChip: {
        styleOverrides: {
          root: {
            borderRadius: 999,
            fontWeight: 600,
          },
        },
      },

      /* ── Dialog ── */
      MuiDialog: {
        styleOverrides: {
          paper: {
            borderRadius: 20,
            boxShadow: isDark ? '0 24px 60px rgba(0,0,0,0.6)' : '0 24px 60px rgba(34,34,34,0.18)',
          },
        },
      },

      /* ── Tooltip ── */
      MuiTooltip: {
        styleOverrides: {
          tooltip: {
            borderRadius: 8,
            fontSize: '0.78rem',
            background: '#333',
          },
        },
      },

      /* ── Select ── */
      MuiSelect: {
        styleOverrides: {
          root: {
            borderRadius: 12,
          },
        },
      },

      /* ── Menu / MenuItem (dropdowns) ── */
      MuiMenu: {
        styleOverrides: {
          paper: {
            borderRadius: 12,
            background: isDark ? tokens.bg1 : paperBg,
            boxShadow: isDark ? '0 12px 36px rgba(0,0,0,0.6)' : undefined,
            color: textPrimary,
            padding: '6px 0',
          },
        },
      },

      MuiMenuItem: {
        styleOverrides: {
          root: {
            color: textPrimary,
            borderRadius: 8,
            paddingTop: 8,
            paddingBottom: 8,
            '&.Mui-selected': {
              backgroundColor: isDark ? 'rgba(255,122,182,0.08)' : 'rgba(255,122,182,0.08)'
            },
            '&:hover': {
              backgroundColor: isDark ? 'rgba(255,122,182,0.06)' : 'rgba(255,122,182,0.06)'
            }
          }
        }
      },

      /* ── Table ── */
      MuiTableHead: {
        styleOverrides: {
          root: {
            '& .MuiTableCell-head': {
              fontWeight: 700,
              color: '#444',
              background: 'rgba(255,122,182,0.06)',
            },
          },
        },
      },

      MuiTableRow: {
        styleOverrides: {
          root: {
            '&:last-child td, &:last-child th': { border: 0 },
            '&:hover': { background: 'rgba(255,122,182,0.04)' },
          },
        },
      },

      /* ── AppBar ── */
      MuiAppBar: {
        styleOverrides: {
          root: {
            background: appBarBg,
            backdropFilter: 'blur(16px)',
            borderBottom: appBarBorder,
            boxShadow: '0 2px 12px rgba(34,34,34,0.06)',
            color: appBarColor,
          },
        },
      },

      /* ── Linear progress ── */
      MuiLinearProgress: {
        styleOverrides: {
          root: {
            borderRadius: 999,
            height: 10,
            background: 'rgba(0,0,0,0.06)',
          },
          bar: {
            borderRadius: 999,
            background: `linear-gradient(90deg, ${tokens.accent2}, ${tokens.accent})`,
          },
        },
      },

      /* ── Snackbar / Alert ── */
      MuiAlert: {
        styleOverrides: {
          root: {
            borderRadius: 12,
            fontWeight: 500,
          },
        },
      },
    },
  })
}
