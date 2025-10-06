import { CssBaseline, ThemeProvider, createTheme } from '@mui/material'
import type { PaletteMode, ThemeOptions } from '@mui/material'
import type { PropsWithChildren } from 'react'

const lightPalette: ThemeOptions['palette'] = {
  mode: 'light',
  primary: {
    main: '#5b4ef2',
    light: '#8b82ff',
    dark: '#433dcc',
  },
  secondary: {
    main: '#0dad8d',
    light: '#4bd5b8',
    dark: '#078b71',
  },
  background: {
    default: '#f5f6fa',
    paper: '#ffffff',
  },
  text: {
    primary: '#1b1f3b',
    secondary: '#4c5270',
  },
}

const darkPalette: ThemeOptions['palette'] = {
  mode: 'dark',
  primary: {
    main: '#8c8fff',
    light: '#b3b6ff',
    dark: '#5a5fc7',
  },
  secondary: {
    main: '#26d7b4',
    light: '#5cf5d3',
    dark: '#00a688',
  },
  background: {
    default: '#0f172a',
    paper: '#111c34',
  },
  text: {
    primary: '#f8fafc',
    secondary: '#cbd5f5',
  },
}

function buildThemeOptions(mode: PaletteMode): ThemeOptions {
  const palette = (mode === 'light' ? lightPalette : darkPalette) ?? {}

  return {
    palette,
    typography: {
      fontFamily: 'Inter, "Segoe UI", SFProDisplay, system-ui, sans-serif',
      h1: { fontWeight: 600, fontSize: '2.5rem' },
      h2: { fontWeight: 600, fontSize: '2rem' },
      h3: { fontWeight: 600, fontSize: '1.75rem' },
      h4: { fontWeight: 600, fontSize: '1.5rem' },
      h5: { fontWeight: 600, fontSize: '1.25rem' },
      h6: { fontWeight: 600, fontSize: '1.1rem' },
      subtitle1: { fontWeight: 500 },
      button: { fontWeight: 600, textTransform: 'none' },
    },
    shape: {
      borderRadius: 12,
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          body: {
            backgroundColor: palette.background && palette.background.default ? palette.background.default : undefined,
            color: palette.text && palette.text.primary ? palette.text.primary : undefined,
          },
        },
      },
      MuiPaper: {
        defaultProps: {
          elevation: 0,
        },
        styleOverrides: {
          root: {
            backgroundImage: 'none',
          },
        },
      },
      MuiButton: {
        styleOverrides: {
          root: {
            borderRadius: 999,
            paddingInline: '1.25rem',
            paddingBlock: '0.65rem',
          },
        },
      },
      MuiAppBar: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
          },
        },
      },
      MuiCard: {
        defaultProps: {
          elevation: 0,
        },
        styleOverrides: {
          root: {
            borderRadius: 16,
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: {
            borderRadius: 999,
          },
        },
      },
    },
  }
}

export function createAppTheme(mode: PaletteMode) {
  return createTheme(buildThemeOptions(mode))
}

export function ThemedContainer({ children, mode }: PropsWithChildren<{ mode: PaletteMode }>) {
  const theme = createAppTheme(mode)

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  )
}
