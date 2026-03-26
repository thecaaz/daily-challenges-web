import { test, expect } from '@playwright/test'
import { loginAsAdmin } from '../test-utils'

test('admin can create a new game', async ({ page }) => {
  await loginAsAdmin(page)

  // Go to admin page
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()

  // Fill create form
  const uniqueName = `e2e-game-${Date.now()}`
  await page.fill('label:has-text("Game name") >> xpath=.. >> input', uniqueName).catch(async () => {
    // fallback: try TextField by role
    await page.fill('input[aria-label="Game name"]', uniqueName)
  })

  // Optionally set reset time
  await page.fill('input[type="time"]', '00:00')

  // Submit form
  await Promise.all([
    page.waitForResponse(resp => resp.url().endsWith('/api/games') && (resp.status() === 200 || resp.status() === 201)),
    page.click('button:has-text("Create Game")')
  ])
  await page.waitForTimeout(500) // wait for SPA update after game creation

  // After creation, navigate to games list and assert the new game is visible
  await page.goto('/')
  await expect(page.locator(`text=${uniqueName}`)).toBeVisible()
})
