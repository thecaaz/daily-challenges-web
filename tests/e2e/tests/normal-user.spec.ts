import { test, expect } from '@playwright/test'
import { loginAsUser } from '../test-utils'

test('normal user cannot see admin link and is redirected from /admin', async ({ page }) => {
  // Login via UI (user seeded by globalSetup)
  await loginAsUser(page)

  // After login, the admin link should not be present
  const adminLink = page.locator('text=Admin')
  await expect(adminLink).toHaveCount(0)

  // Attempt to visit /admin should redirect away (Admin page shows 'Manage Games' when allowed)
  await page.goto('/admin')
  const manageGames = page.locator('text=Manage Games')
  await expect(manageGames).toHaveCount(0)
})
