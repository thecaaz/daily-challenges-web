import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame, openGameByName } from '../test-utils'

test('admin cannot see today\'s scores if he has not submitted yet', async ({ page }) => {
  // Admin: login and create a new game via UI helper
  await loginAsAdmin(page)

  const providedName = `e2e-hide-today-${Date.now()}`
  const { gameId, gameName } = await createGame(page, providedName)

  // Ensure we're on the game's page and assert today's scores are hidden
  await openGameByName(page, gameName)
  await expect(page.locator("text=Today's scores are hidden.")).toBeVisible()
})
