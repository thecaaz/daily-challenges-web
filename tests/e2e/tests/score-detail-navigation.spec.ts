import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame, openSubmitForGame, openGameByName } from '../test-utils'

// Increase timeout for stability
test.setTimeout(60_000)

test('clicking a score navigates to submission detail', async ({ page }) => {
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page, undefined, { navigateToGame: false })

  // Navigate to submit page via UI-only helpers
  await openSubmitForGame(page, gameName)
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

  // Visit the game's page via UI-only navigation and ensure the score appears
  await openGameByName(page, gameName)
  // Wait for the score to appear by matching digits (ignore locale separators)
  await page.waitForFunction((sv) => {
    return Array.from(document.querySelectorAll('a, h6')).some(el => (el.textContent || '').replace(/\D/g, '') === sv)
  }, scoreValue, { timeout: 5000 })
  // Click the matching score via its heading element's ancestor link
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
  await page.waitForFunction(() => location.pathname.includes('/submission/'), null, { timeout: 15000 })
  const match = page.url().match(/\/submission\/(\d+)/)
  expect(match).not.toBeNull()
})
