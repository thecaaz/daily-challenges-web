import { hexToRgb } from './modeColors'

/**
 * MUI component style overrides, grouped by component.
 *
 * @param {{ tokens: object, isDark: boolean, colors: object }} context
 *   design tokens, mode, and the mode colours from getModeColors
 */
export default function createComponentOverrides(context) {
  return {
    ...buttonOverrides(context),
    ...cardOverrides(context),
    ...paperOverrides(context),
    ...textFieldOverrides(context),
    ...chipOverrides(context),
    ...dialogOverrides(context),
    ...tooltipOverrides(context),
    ...selectOverrides(context),
    ...menuOverrides(context),
    ...tableOverrides(context),
    ...appBarOverrides(context),
    ...linearProgressOverrides(context),
    ...alertOverrides(context),
  }
}

/** Disabled primary buttons keep readable contrast while looking disabled. */
function readableDisabledPrimary(tokens) {
  return {
    background: `linear-gradient(90deg, rgba(${hexToRgb(tokens.accent)},0.36), rgba(${hexToRgb(tokens.accent2)},0.36))`,
    color: 'rgba(255,255,255,0.95)',
    boxShadow: 'none',
    textShadow: 'none',
  }
}

function buttonOverrides({ tokens, isDark }) {
  return {
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
          '&.Mui-disabled': readableDisabledPrimary(tokens),
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
  }
}

function cardOverrides({ tokens, isDark, colors }) {
  return {
    MuiCard: {
      styleOverrides: {
        root: {
          background: colors.cardBg,
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
  }
}

function paperOverrides({ isDark, colors }) {
  return {
    MuiPaper: {
      styleOverrides: {
        root: {
          borderRadius: 18,
          background: colors.paperBg,
          boxShadow: isDark ? '0 8px 24px rgba(0,0,0,0.6)' : '0 8px 24px rgba(34,34,34,0.08)',
        },
        elevation0: { boxShadow: 'none' },
      },
    },
  }
}

function outlinedInputRoot({ tokens, isDark, colors }) {
  return {
    borderRadius: 12,
    background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(255,255,255,0.9)',
    '& fieldset': { borderColor: colors.inputBorder },
    '&:hover fieldset': { borderColor: tokens.accent },
    '&.Mui-focused fieldset': { borderColor: tokens.accent, borderWidth: 2 },
  }
}

function textFieldOverrides(context) {
  return {
    MuiTextField: {
      defaultProps: { variant: 'outlined', fullWidth: true },
      styleOverrides: {
        root: {
          '& .MuiOutlinedInput-root': outlinedInputRoot(context),
          '& .MuiInputLabel-root.Mui-focused': { color: context.tokens.accent },
        },
      },
    },

    MuiOutlinedInput: {
      styleOverrides: {
        root: outlinedInputRoot(context),
      },
    },
  }
}

function chipOverrides() {
  return {
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 999,
          fontWeight: 600,
        },
      },
    },
  }
}

function dialogOverrides({ isDark }) {
  return {
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: 20,
          boxShadow: isDark ? '0 24px 60px rgba(0,0,0,0.6)' : '0 24px 60px rgba(34,34,34,0.18)',
        },
      },
    },
  }
}

function tooltipOverrides() {
  return {
    MuiTooltip: {
      styleOverrides: {
        tooltip: {
          borderRadius: 8,
          fontSize: '0.78rem',
          background: '#333',
        },
      },
    },
  }
}

function selectOverrides() {
  return {
    MuiSelect: {
      styleOverrides: {
        root: {
          borderRadius: 12,
        },
      },
    },
  }
}

function menuOverrides({ tokens, isDark, colors }) {
  return {
    MuiMenu: {
      styleOverrides: {
        paper: {
          borderRadius: 12,
          background: isDark ? tokens.bg1 : colors.paperBg,
          boxShadow: isDark ? '0 12px 36px rgba(0,0,0,0.6)' : undefined,
          color: colors.textPrimary,
          padding: '6px 0',
        },
      },
    },

    MuiMenuItem: {
      styleOverrides: {
        root: {
          color: colors.textPrimary,
          borderRadius: 8,
          paddingTop: 8,
          paddingBottom: 8,
          '&.Mui-selected': {
            backgroundColor: 'rgba(255,122,182,0.08)'
          },
          '&:hover': {
            backgroundColor: 'rgba(255,122,182,0.06)'
          }
        }
      }
    },
  }
}

function tableOverrides({ isDark, colors }) {
  return {
    MuiTableHead: {
      styleOverrides: {
        root: {
          '& .MuiTableCell-head': {
            fontWeight: 700,
            color: colors.tableHeadColor,
            background: isDark ? 'rgba(255,122,182,0.10)' : 'rgba(255,122,182,0.06)',
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
  }
}

function appBarOverrides({ colors }) {
  return {
    MuiAppBar: {
      styleOverrides: {
        root: {
          background: colors.appBarBg,
          backdropFilter: 'blur(16px)',
          borderBottom: colors.appBarBorder,
          boxShadow: '0 2px 12px rgba(34,34,34,0.06)',
          color: colors.appBarColor,
        },
      },
    },
  }
}

function linearProgressOverrides({ tokens, isDark }) {
  return {
    MuiLinearProgress: {
      styleOverrides: {
        root: {
          borderRadius: 999,
          height: 10,
          background: isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)',
        },
        bar: {
          borderRadius: 999,
          background: `linear-gradient(90deg, ${tokens.accent2}, ${tokens.accent})`,
          boxShadow: isDark ? `0 6px 18px rgba(${hexToRgb(tokens.accent)},0.14)` : undefined,
        },
      },
    },
  }
}

function alertOverrides() {
  return {
    MuiAlert: {
      styleOverrides: {
        root: {
          borderRadius: 12,
          fontWeight: 500,
        },
      },
    },
  }
}
