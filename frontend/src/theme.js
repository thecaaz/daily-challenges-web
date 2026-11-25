import { createTheme } from '@mui/material/styles'
import getTokens from './styles/tokens'
import getModeColors from './styles/theme/modeColors'
import createPalette from './styles/theme/palette'
import createTypography from './styles/theme/typography'
import shadows from './styles/theme/shadows'
import createComponentOverrides from './styles/theme/componentOverrides'

const BORDER_RADIUS = 14

/** The app's MUI theme for 'light' or 'dark' mode, built from the design tokens. */
export default function createAppTheme(mode = 'light') {
  const tokens = getTokens(mode)
  const isDark = mode === 'dark'
  const colors = getModeColors(tokens, isDark)

  return createTheme({
    palette: createPalette(mode, tokens, colors),
    typography: createTypography(tokens),
    shape: { borderRadius: BORDER_RADIUS },
    shadows,
    components: createComponentOverrides({ tokens, isDark, colors }),
  })
}
