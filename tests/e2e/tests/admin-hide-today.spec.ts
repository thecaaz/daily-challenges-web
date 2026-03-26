import { test, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

test('admin cannot see today\'s scores if he has not submitted yet', async ({ page }) => {
  const credsPath = path.resolve(__dirname, '..', 'credentials.json')
  const credsRaw = await fs.readFile(credsPath, 'utf-8')
  const adminCreds = JSON.parse(credsRaw)

  // Admin: login and create a new game
  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', adminCreds.username)
  await page.fill('input[placeholder="Password"]', adminCreds.password)
  await page.click('button[type="submit"]')
  await page.waitForResponse(resp => resp.url().endsWith('/api/auth/login'))

  const gameName = `e2e-hide-today-${Date.now()}`
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()
  // Fill create form
  await page.fill('label:has-text("Game name") >> xpath=.. >> input', gameName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', gameName)
  })
  await page.fill('input[type="time"]', '00:00')
  await Promise.all([
    page.waitForResponse(resp => resp.url().endsWith('/api/games') && (resp.status() === 200 || resp.status() === 201)),
    page.click('button:has-text("Create Game")')
  ])

  // Navigate to home and click the created game to get to its page
  await page.goto('/')
  await page.click(`text=${gameName}`)
  // Now we should be on /games/:id
  const url = page.url()
  const match = url.match(/\/games\/(\d+)/)
  if (!match) throw new Error('could not determine game id from URL: ' + url)
  const gameId = match[1]

  // As admin (who has not submitted), visit the game's submissions page and assert today's scores are hidden
  await page.goto(`/games/${gameId}`)
  await expect(page.locator("text=Today's scores are hidden.")).toBeVisible()
})
