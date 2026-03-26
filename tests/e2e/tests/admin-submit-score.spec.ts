import { test, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

// Increase per-file test timeout to reduce flakiness in CI
test.setTimeout(60_000)

test('admin can submit a score and it appears for today', async ({ page }) => {
  const credsPath = path.resolve(__dirname, '..', 'credentials.json')
  const credsRaw = await fs.readFile(credsPath, 'utf-8')
  const adminCreds = JSON.parse(credsRaw)

  // Login as admin
  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', adminCreds.username)
  await page.fill('input[placeholder="Password"]', adminCreds.password)
  await page.click('button[type="submit"]')
  await page.waitForResponse(resp => resp.url().endsWith('/api/auth/login'))

  // Create a new game
  const gameName = `e2e-submit-${Date.now()}`
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()
  await page.fill('label:has-text("Game name") >> xpath=.. >> input', gameName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', gameName)
  })
  await page.fill('input[type="time"]', '00:00')
  await Promise.all([
    page.waitForResponse(resp => resp.url().endsWith('/api/games') && (resp.status() === 200 || resp.status() === 201)),
    page.click('button:has-text("Create Game")')
  ])

  // Open the game page to determine id
  await page.goto('/')
  await page.click(`text=${gameName}`)
  const url = page.url()
  const match = url.match(/\/games\/(\d+)/)
  if (!match) throw new Error('could not determine game id from URL: ' + url)
  const gameId = match[1]

  // Go to submit page and submit a score
  await page.goto(`/submit/${gameId}`)
  await expect(page.locator('text=Submit for')).toBeVisible()

  const scoreValue = String(Math.floor(Math.random() * 100000))
  // Prefer semantic selectors (role/label). Fall back to placeholder/attribute selectors.
  await page.getByRole('textbox', { name: 'Score' }).fill(scoreValue).catch(async () => {
    await page.getByLabel('Score').fill(scoreValue).catch(async () => {
      await page.fill('input[placeholder="Score"]', scoreValue).catch(async () => {
        await page.fill('input[type="text"]', scoreValue).catch(() => {})
      })
    })
  })

  // Click submit using a robust locator and wait for SPA route change
  const submitBtn = page.getByRole('button', { name: 'Submit' })
  // Make interaction resilient: wait for attachment/visibility but tolerate transient failures
  await submitBtn.waitFor({ state: 'attached', timeout: 5000 }).catch(() => {})
  await submitBtn.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {})
  await submitBtn.scrollIntoViewIfNeeded().catch(() => {})
  await submitBtn.click({ timeout: 10000 })
  await page.waitForFunction((id) => location.pathname.includes(`/games/${id}`), gameId, { timeout: 15000 })

  // After submission, visit the game's submissions page and assert the submitted score is visible
  await page.goto(`/games/${gameId}`)
  // Ensure hidden message is not present
  await expect(page.locator("text=Today's scores are hidden.")).toHaveCount(0)
  // Assert the score appears
  await expect(page.locator(`text=${scoreValue}`)).toBeVisible()
})