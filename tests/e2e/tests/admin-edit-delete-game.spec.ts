import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame } from '../test-utils'

test('admin can edit and delete a game via admin UI', async ({ page }) => {
  // Login and create a game via the UI helper
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page)

  // Open admin page and locate the game's card
  await page.goto('/admin')
  const strong = page.locator('strong', { hasText: gameName }).first()
  await expect(strong).toBeVisible()
  const gameContainer = strong.locator('xpath=ancestor::div[.//button[@title="Edit"]][1]')
  const editBtn = gameContainer.locator('button[title="Edit"]').first()
  await expect(editBtn).toBeVisible()
  await editBtn.scrollIntoViewIfNeeded().catch(() => {})
  await editBtn.click()

  // Wait for the edit panel to appear
  const editPanel = page.locator('xpath=//h3[normalize-space(.)="Edit Game"]/ancestor::div[1]').first()
  await expect(editPanel).toBeVisible()

  // New values to apply
  const newName = `${gameName} (edited)`
  const newUrl = `https://example.com/${Date.now()}`
  const newTime = '12:34'

  // Fill name (robust selector with fallback)
  await editPanel.locator('label:has-text("Name") >> xpath=.. >> input').fill(newName).catch(async () => {
    await editPanel.locator('input[aria-label="Name"]').fill(newName).catch(() => {})
  })

  // Fill URL
  await editPanel.locator('label:has-text("URL") >> xpath=.. >> input').fill(newUrl).catch(async () => {
    await editPanel.locator('input[aria-label="URL"]').fill(newUrl).catch(() => {})
  })

  // Fill reset time (scope to the edit panel)
  await editPanel.locator('input[type="time"]').first().fill(newTime).catch(() => {})

  // Save changes and wait for the PUT request to complete
  await Promise.all([
    page.waitForResponse(r => r.url().endsWith(`/api/games/${gameId}`) && r.request().method() === 'PUT'),
    editPanel.locator('button:has-text("Save")').click()
  ])

  // Verify updated name appears in admin list
  await expect(page.locator('strong', { hasText: newName })).toBeVisible()

  // Verify changes on the public game page
  await page.goto(`/games/${gameId}`)
  await expect(page.locator(`text=${newName}`)).toBeVisible()
  // Scope the Play link to the game's header so we don't match the global nav
  const header = page.locator('h5', { hasText: `Submissions — ${newName}` }).first()
  const playLink = header.locator('xpath=ancestor::div[1]//a[normalize-space(.)="Play"]').first()
  await expect(playLink).toHaveAttribute('href', newUrl)

  // Delete the game via admin UI (assert confirm dialog text and accept)
  await page.goto('/admin')
  const strong2 = page.locator('strong', { hasText: newName }).first()
  const gameContainer2 = strong2.locator('xpath=ancestor::div[.//button[@title="Delete"]][1]')
  const deleteBtn = gameContainer2.locator('button[title="Delete"]').first()
  await expect(deleteBtn).toBeVisible()
  await deleteBtn.scrollIntoViewIfNeeded().catch(() => {})
  await deleteBtn.hover().catch(() => {})

  const deleteRespPromise = page.waitForResponse(r => r.url().endsWith(`/api/games/${gameId}`) && r.request().method() === 'DELETE')

  // Attach a one-time dialog handler before clicking so we reliably capture the confirm
  let dialogSeen = false
  page.once('dialog', async (dialog) => {
    dialogSeen = true
    expect(dialog.type()).toBe('confirm')
    expect(dialog.message()).toBe('Delete game? This will remove submissions.')
    await dialog.accept()
  })

  // Click with a small timeout; if the native click hangs for some reason, fallback to JS click
  try {
    await deleteBtn.click({ timeout: 10000 })
  } catch (e) {
    const handle = await deleteBtn.elementHandle()
    if (handle) {
      await page.evaluate((el: any) => (el as HTMLElement).click(), handle).catch(() => {})
    } else {
      throw e
    }
  }

  const delResp = await deleteRespPromise
  expect(dialogSeen).toBeTruthy()
  expect([200, 204]).toContain(delResp.status())

  // Verify the game is removed from admin list and home page
  await expect(page.locator('strong', { hasText: newName })).toHaveCount(0)
  await page.goto('/')
  await expect(page.locator(`text=${newName}`)).toHaveCount(0)
})
