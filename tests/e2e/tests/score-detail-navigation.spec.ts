import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame } from '../test-utils'

// Increase timeout for stability
test.setTimeout(60_000)

test('clicking a score navigates to submission detail', async ({ page }) => {
  await loginAsAdmin(page)
  const { gameId } = await createGame(page)

  // Go to submit page
  await page.goto(`/submit/${gameId}`)
  await expect(page.locator('text=Submit for')).toBeVisible()

  const scoreValue = String(Math.floor(Math.random() * 100000))

  await page.getByRole('textbox', { name: 'Score' }).fill(scoreValue).catch(async () => {
    await page.getByLabel('Score').fill(scoreValue).catch(async () => {
      await page.fill('input[placeholder="Score"]', scoreValue).catch(async () => {
        await page.fill('input[type="text"]', scoreValue).catch(() => {})
      })
    })
  })

  const submitBtn = page.getByRole('button', { name: 'Submit' })
  await submitBtn.waitFor({ state: 'attached', timeout: 5000 }).catch(() => {})
  await submitBtn.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {})
  await submitBtn.scrollIntoViewIfNeeded().catch(() => {})

  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions')),
    submitBtn.click({ timeout: 10000 })
  ])
  expect(resp.status()).toBeGreaterThanOrEqual(200)
  expect(resp.status()).toBeLessThan(300)

  // Visit the game's page and ensure the score appears
  await page.goto(`/games/${gameId}`)
  await expect(page.locator(`text=${scoreValue}`)).toBeVisible()

  // Click the score and assert navigation to /submission/:id
  await page.click(`text=${scoreValue}`)
  await page.waitForFunction(() => location.pathname.includes('/submission/'), null, { timeout: 15000 })
  const match = page.url().match(/\/submission\/(\d+)/)
  expect(match).not.toBeNull()
})
