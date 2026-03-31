import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame, openSubmitForGame, openHomeViaUI, openRegisterViaUI, login } from '../test-utils'
import { randomUUID } from 'crypto'

// Increase per-file test timeout to reduce flakiness in CI
test.setTimeout(60_000)

test('duplicate submission attempt via UI shows an error/notification', async ({ page }) => {
  // Create a game as admin
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page)

  // Now create a fresh user for this test to avoid cross-test interference, then submit a score via the UI
  const username = `e2e-user-${Date.now()}-${randomUUID()}`
  const password = 'P@ssw0rd!'
  // Log out the admin user so the Register link is visible, then open the register form
  await page.click('button:has-text("Logout")').catch(() => {})
  await openRegisterViaUI(page)
  // Fill registration form via accessible labels
  await page.getByRole('textbox', { name: 'Username' }).fill(username)
  await page.getByRole('textbox', { name: 'Password' }).fill(password)
  const [regResp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/auth/register') && (r.status() >= 200 && r.status() < 400)),
    page.click('button[type="submit"]')
  ])
  expect([200, 201]).toContain(regResp.status())
  await login(page, { username, password })
  // Ensure the public games listing is visible for the normal user before attempting to open the submit flow
  await openHomeViaUI(page)
  // Wait until the created game's card appears in the public listing
  await page.locator(`a:has-text("${gameName}")`).first().waitFor({ timeout: 15000 })
  await openSubmitForGame(page, gameName)
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
  // NOTE: the public submissions page may disable the Submit button after a successful submit
  // (hasSubmittedForLatest). As a minimal, documented exception we navigate directly to the
  // submit route to reproduce the duplicate-submission server-side handling.
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
