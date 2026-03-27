import { test, expect } from '@playwright/test'
import { loginAsAdmin, loginAsUser, createGame } from '../test-utils'

// Increase per-file test timeout to reduce flakiness in CI
test.setTimeout(60_000)

test('duplicate submission attempt via UI shows an error/notification', async ({ page }) => {
  // Create a game as admin
  await loginAsAdmin(page)
  const { gameId } = await createGame(page)

  // Now login as a normal user and submit a score via the UI
  await loginAsUser(page)
  await page.goto(`/submit/${gameId}`)
  await expect(page.locator('text=Submit for')).toBeVisible()

  const firstScore = String(Math.floor(Math.random() * 100000))
  await page.getByRole('textbox', { name: 'Score' }).fill(firstScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', firstScore)
  })

  const submitBtn = page.getByRole('button', { name: 'Submit' })
  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && (r.status() >= 200 && r.status() < 400)),
    submitBtn.click({ timeout: 10000 })
  ])
  expect(resp.status()).toBeGreaterThanOrEqual(200)
  expect(resp.status()).toBeLessThan(400)

  // Navigate back to the submit page and attempt to submit again
  await page.goto(`/submit/${gameId}`)
  await expect(page.locator('text=Submit for')).toBeVisible()

  const secondScore = String(Math.floor(Math.random() * 100000))
  await page.getByRole('textbox', { name: 'Score' }).fill(secondScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', secondScore)
  })

  // Click submit and assert a visible "already submitted" user-facing message
  await page.getByRole('button', { name: 'Submit' }).click()
  // Accept either client-side or server-side wording by matching the common phrase
  await expect(page.locator('text=/already submitted/i')).toBeVisible({ timeout: 5000 })
})
