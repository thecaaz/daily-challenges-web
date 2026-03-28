import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame } from '../test-utils'

test('admin can create a new game', async ({ page }) => {
  await loginAsAdmin(page)

  // Use the shared helper which performs a UI-only create and waits for the listing to update
  const { gameId, gameName } = await createGame(page)

  // Ensure the created game is visible on the public listing
  await page.goto('/')
  await expect(page.locator(`a:has-text("${gameName}")`).first()).toBeVisible({ timeout: 15000 })
})
