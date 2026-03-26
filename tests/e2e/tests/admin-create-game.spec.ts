import { test, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

test('admin can create a new game', async ({ page }) => {
  const credsPath = path.resolve(__dirname, '..', 'credentials.json')
  const credsRaw = await fs.readFile(credsPath, 'utf-8')
  const creds = JSON.parse(credsRaw)

  // Login via UI
  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', creds.username)
  await page.fill('input[placeholder="Password"]', creds.password)
  await page.click('button[type="submit"]')
  const loginResp = await page.waitForResponse(resp => resp.url().endsWith('/api/auth/login'))
  expect(loginResp.status()).toBe(200)

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
