import { test, expect } from '@playwright/test'
import path from 'path'
import { loginAsAdmin } from '../test-utils'

test('admin can create game with image via UI', async ({ page }) => {
  await loginAsAdmin(page)

  await page.goto('/admin')

  const uniqueName = `e2e-game-img-${Date.now()}`
  await page.fill('label:has-text("Game name") >> xpath=.. >> input', uniqueName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', uniqueName)
  })

  await page.fill('input[type="time"]', '00:00')

  const imgPath = path.resolve(__dirname, '../test-data/test-image.png')
  // Attach image to file input
  await page.setInputFiles('input[type="file"]', imgPath).catch(async () => {
    await page.locator('input[type="file"]').first().setInputFiles(imgPath)
  })

  await Promise.all([
    page.waitForResponse(resp => resp.url().endsWith('/api/games') && (resp.status() >= 200 && resp.status() < 400)),
    page.click('button:has-text("Create Game")')
  ])

  await page.waitForTimeout(500)
  await page.goto('/')
  await expect(page.locator(`text=${uniqueName}`)).toBeVisible()
  await expect(page.locator('img')).toBeVisible()
})
