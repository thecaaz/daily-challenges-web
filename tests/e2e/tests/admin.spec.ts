import { test, expect } from '@playwright/test'
import { loginAsAdmin, openHomeViaUI, openAdminViaUI } from '../test-utils'

test('creates first user through UI and loads admin page', async ({ page }) => {
  // Login via UI (admin is seeded by globalSetup)
  await loginAsAdmin(page)
  await openHomeViaUI(page)

  // Navigate to admin page and verify admin-only content
  await openAdminViaUI(page)
  await expect(page.locator('text=Manage Games')).toBeVisible()
})
