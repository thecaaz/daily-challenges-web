import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame, openSubmitForGame, openGameByName } from '../test-utils'

// Increase per-file test timeout to reduce flakiness in CI
test.setTimeout(60_000)

test('admin can submit a score and it appears for today', async ({ page }) => {
  await loginAsAdmin(page)

  // Create a new game via helper
  const gameName = `e2e-submit-${Date.now()}`
  const { gameId, gameName: createdName } = await createGame(page, gameName)

  // Go to submit page and submit a score via UI
  await openSubmitForGame(page, createdName)
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

  // After submission, visit the game's submissions page via UI and assert the submitted score is visible
  await openGameByName(page, createdName)
  // Ensure hidden message is not present
  await expect(page.locator("text=Today's scores are hidden.")).toHaveCount(0)
  // Assert the score appears (match digits ignoring locale-specific separators)
  await page.waitForFunction((sv) => {
    return Array.from(document.querySelectorAll('a, h6')).some(el => (el.textContent || '').replace(/\D/g, '') === sv)
  }, scoreValue, { timeout: 5000 })
  // Find the score in the page's headings and click its ancestor link
  const h6s = page.locator('h6')
  const h6count = await h6s.count()
  let clicked = false
  for (let i = 0; i < h6count; i++) {
    const txt = (await h6s.nth(i).textContent()) ?? ''
    if (txt.replace(/\D/g, '') === scoreValue) {
      const anc = h6s.nth(i).locator('xpath=ancestor::a[1]').first()
      await anc.click()
      clicked = true
      break
    }
  }
  if (!clicked) throw new Error(`Could not find submission link for score ${scoreValue}`)
})