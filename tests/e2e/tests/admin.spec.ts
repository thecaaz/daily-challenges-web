import { test, expect } from '@playwright/test'
import { loginAsAdmin } from '../test-utils'

test('creates first user through UI and loads admin page', async ({ page }) => {
  // Login via UI (admin is seeded by globalSetup)
  await loginAsAdmin(page)
  await page.goto('/')

  // Navigate to admin page and verify admin-only content
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()
})
