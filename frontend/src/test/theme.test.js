import { describe, it, expect } from 'vitest'
import createAppTheme from '../theme'

const themedParts = ({ palette, typography, shape, shadows, components }) =>
  ({ palette, typography, shape, shadows, components })

describe('createAppTheme', () => {
  it.each(['light', 'dark'])('builds the %s theme unchanged', mode => {
    expect(themedParts(createAppTheme(mode))).toMatchSnapshot()
  })

  it('defaults to the light theme', () => {
    expect(JSON.stringify(themedParts(createAppTheme()))).toBe(JSON.stringify(themedParts(createAppTheme('light'))))
  })
})
