import { test, expect } from '@playwright/test'
import { loginAsAdmin, loginAsUser, createGame } from '../test-utils'

test('admin can view unfiltered submissions for a game via admin UI', async ({ page }) => {
  // Login as admin and create a game
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page)

  // Create a submission as admin
  await page.goto(`/submit/${gameId}`)
  const adminScore = `admin-${Date.now() % 100000}`
  await page.getByRole('textbox', { name: 'Score' }).fill(adminScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', adminScore)
  })
  await page.getByRole('button', { name: 'Submit' }).click()
  await page.waitForTimeout(500)

  // Logout admin and login as normal user to create another submission
  await page.click('button:has-text("Logout")')
  await loginAsUser(page)
  await page.goto(`/submit/${gameId}`)
  const userScore = `user-${Date.now() % 100000}`
  await page.getByRole('textbox', { name: 'Score' }).fill(userScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', userScore)
  })
  await page.getByRole('button', { name: 'Submit' }).click()
  await page.waitForTimeout(500)

  // Login back as admin and open admin page
  await page.click('button:has-text("Logout")')
  await loginAsAdmin(page)
  await page.goto('/admin')

    // Click the Manage Submissions button for the created game's card and wait for rows to load
    // Find the <strong> element that contains the game's name, then find its ancestor container that has the Manage Submissions button.
    const strong = page.locator('strong', { hasText: gameName }).first()
    const gameContainer = strong.locator('xpath=ancestor::div[.//button[contains(normalize-space(.), "Manage Submissions")]][1]')
    const manageBtn = gameContainer.locator('button:has-text("Manage Submissions")')
    await expect(manageBtn).toBeVisible()
    await manageBtn.scrollIntoViewIfNeeded().catch(() => {})
    await manageBtn.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {})
    await manageBtn.click()

    // Wait for the submissions table rows to appear (admin UI loads them after clicking)
    const rows = page.locator('table tbody tr')
    await rows.first().waitFor({ timeout: 5000 })

    // Ensure the table is visible by scrolling to the bottom of the page, then scroll the last row into view
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
    await page.waitForTimeout(200)
    await rows.last().scrollIntoViewIfNeeded().catch(() => {})

  // Assert both submissions are present in the admin table
  // The scores are in editable inputs — inspect each table row's input value to find matches.
  const count = await rows.count()
  let foundAdmin = false
  let foundUser = false
  for (let i = 0; i < count; i++) {
    const row = rows.nth(i)
    const input = row.locator('input').first()
    const val = await input.inputValue().catch(() => '')
    if (val === adminScore) {
      await input.scrollIntoViewIfNeeded().catch(() => {})
      foundAdmin = true
    }
    if (val === userScore) {
      await input.scrollIntoViewIfNeeded().catch(() => {})
      foundUser = true
    }
  }

  expect(foundAdmin).toBeTruthy()
  expect(foundUser).toBeTruthy()
})
