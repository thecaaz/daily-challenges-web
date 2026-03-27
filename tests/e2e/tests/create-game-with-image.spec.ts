import { test, expect } from '@playwright/test'
import path from 'path'
import { loginAsAdmin } from '../test-utils'

test('admin can create game with image via UI', async ({ page }) => {
  await loginAsAdmin(page)

  await page.goto('/admin')

  const uniqueName = `e2e-game-img-${Date.now()}`
  await page.fill('label:has-text("Game name") >> xpath=.. >> input', uniqueName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', uniqueName)
  })

  await page.fill('input[type="time"]', '00:00')

  const imgPath = path.resolve(__dirname, '../test-data/test-image.png')
  // Attach image to file input
  await page.setInputFiles('input[type="file"]', imgPath).catch(async () => {
    await page.locator('input[type="file"]').first().setInputFiles(imgPath)
  })

  const [resp] = await Promise.all([
    page.waitForResponse(resp => resp.url().endsWith('/api/games') && (resp.status() >= 200 && resp.status() < 400)),
    page.click('button:has-text("Create Game")')
  ])

  // Try to navigate directly to the created game's page when the API returns
  // the created id; otherwise fallback to checking the homepage (with a
  // longer timeout) for the new game entry.
  let createdId: string | undefined
  try {
    const body = await resp.json()
    createdId = body?.id ?? body?.game?.id ?? body?.data?.id
  } catch (e) {}

  // Regardless of whether the API returned the id, check the public games
  // listing for the new game (the image should appear in the listing).
  await page.waitForTimeout(500)
  await page.goto('/')
  await expect(page.locator(`text=${uniqueName}`)).toBeVisible({ timeout: 10000 })

  // Ensure we assert the image that belongs to the created game's card/list item.
  // Find a container element that contains the game's name and then assert it has an <img> descendant.
  const gameImage = page.locator(
    `xpath=//div[.//text()[normalize-space()="${uniqueName}"]]//img`
  )
  await expect(gameImage).toBeVisible()
})
