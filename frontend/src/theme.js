import { createTheme } from '@mui/material/styles'

const theme = createTheme({
  palette: {
    primary: {
      main: '#ff7ab6',
      light: '#ffaad4',
      dark: '#e05090',
      contrastText: '#ffffff',
    },
    secondary: {
      main: '#ffd166',
      light: '#ffe299',
      dark: '#c9a23d',
      contrastText: '#222222',
    },
    background: {
      default: '#fef6f0',
      paper: 'rgba(255,255,255,0.95)',
    },
    text: {
      primary: '#222222',
      secondary: '#6b6b6b',
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
          background: 'linear-gradient(90deg, #ff7ab6, #ffd166)',
          color: '#fff',
          boxShadow: '0 6px 18px rgba(255,122,182,0.22)',
          '&:hover': {
            background: 'linear-gradient(90deg, #ff90c4, #ffe080)',
            boxShadow: '0 10px 24px rgba(255,122,182,0.30)',
          },
        },
        containedSecondary: {
          background: '#ffd166',
          color: '#222',
          boxShadow: '0 6px 18px rgba(255,209,102,0.3)',
          '&:hover': {
            background: '#ffe080',
            boxShadow: '0 10px 24px rgba(255,209,102,0.4)',
          },
        },
        outlinedPrimary: {
          borderColor: '#ff7ab6',
          color: '#ff7ab6',
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
          background: 'linear-gradient(180deg, rgba(255,255,255,0.92), rgba(255,250,250,0.82))',
          borderRadius: 18,
          boxShadow: '0 8px 20px rgba(34,34,34,0.08), inset 0 1px 0 rgba(255,255,255,0.7)',
          border: '1px solid rgba(0,0,0,0.04)',
          transition: 'transform 180ms cubic-bezier(.2,.9,.2,1), box-shadow 180ms',
          '&:hover': {
            transform: 'translateY(-6px)',
            boxShadow: '0 16px 32px rgba(34,34,34,0.13)',
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
          background: 'rgba(255,255,255,0.95)',
          boxShadow: '0 8px 24px rgba(34,34,34,0.08)',
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
            background: 'rgba(255,255,255,0.9)',
            '& fieldset': { borderColor: 'rgba(0,0,0,0.1)' },
            '&:hover fieldset': { borderColor: '#ff7ab6' },
            '&.Mui-focused fieldset': { borderColor: '#ff7ab6', borderWidth: 2 },
          },
          '& .MuiInputLabel-root.Mui-focused': { color: '#ff7ab6' },
        },
      },
    },

    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 12,
          background: 'rgba(255,255,255,0.9)',
          '& fieldset': { borderColor: 'rgba(0,0,0,0.1)' },
          '&:hover fieldset': { borderColor: '#ff7ab6' },
          '&.Mui-focused fieldset': { borderColor: '#ff7ab6', borderWidth: 2 },
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
          boxShadow: '0 24px 60px rgba(34,34,34,0.18)',
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
          background: 'rgba(255,255,255,0.88)',
          backdropFilter: 'blur(16px)',
          borderBottom: '1px solid rgba(0,0,0,0.04)',
          boxShadow: '0 2px 12px rgba(34,34,34,0.06)',
          color: '#222',
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
          background: 'linear-gradient(90deg,#ffd166,#ff7ab6)',
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

export default theme
