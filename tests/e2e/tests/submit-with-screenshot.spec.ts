import { test, expect } from '@playwright/test'
import path from 'path'
import { loginAsAdmin, createGame, openSubmitForGame, openGameByName } from '../test-utils'

test('submit a score with screenshot via UI', async ({ page }) => {
  await loginAsAdmin(page)

  const { gameId, gameName } = await createGame(page, undefined, { navigateToGame: false })

  await openSubmitForGame(page, gameName)
  await expect(page.locator('text=Submit for')).toBeVisible()

  const scoreValue = String(Math.floor(Math.random() * 100000))
  await page.getByRole('textbox', { name: 'Score' }).fill(scoreValue).catch(async () => {
    await page.fill('input[placeholder="Score"]', scoreValue)
  })

  const imgPath = path.resolve(__dirname, '../test-data/test-image.png')
  await page.setInputFiles('input[type="file"]', imgPath).catch(async () => {
    await page.locator('input[type="file"]').first().setInputFiles(imgPath)
  })

  const submitBtn = page.getByRole('button', { name: 'Submit' })
  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && (r.status() >= 200 && r.status() < 400)),
    submitBtn.click({ timeout: 10000 })
  ])
  expect(resp.status()).toBeGreaterThanOrEqual(200)
  expect(resp.status()).toBeLessThan(400)

  // Visit game page and assert the submitted score is present; open the submission and assert screenshot visible
  await openGameByName(page, gameName)
  // Match score by digits (ignore locale separators) and click the submission link
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
  await expect(page.locator('img')).toBeVisible()
})
