import { test, expect } from '@playwright/test'
import path from 'path'
import { loginAsAdmin, createGame } from '../test-utils'

test('submit a score with screenshot via UI', async ({ page }) => {
  await loginAsAdmin(page)

  const { gameId } = await createGame(page, undefined, { navigateToGame: false })

  await page.goto(`/submit/${gameId}`)
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
  await page.goto(`/games/${gameId}`)
  await expect(page.locator(`text=${scoreValue}`)).toBeVisible()
  await page.click(`text=${scoreValue}`)
  await expect(page.locator('img')).toBeVisible()
})
