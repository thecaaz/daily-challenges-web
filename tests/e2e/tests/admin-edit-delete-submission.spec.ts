import { test, expect } from '@playwright/test'
import { loginAsAdmin, loginAsUser, createGame } from '../test-utils'

test('admin can edit and delete a submission via admin UI', async ({ page }) => {
  // Login as admin and create a game via UI
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page, undefined, { navigateToGame: false })

  // Logout admin and create a submission as a normal user (capture created id)
  await page.click('button:has-text("Logout")')
  await loginAsUser(page)
  await page.goto(`/submit/${gameId}`)
  const originalScore = `score-${Date.now() % 100000}`
  await page.getByRole('textbox', { name: 'Score' }).fill(originalScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', originalScore)
  })

  const [postResp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Submit' }).click()
  ])
  expect([200, 201]).toContain(postResp.status())
  const postBody = await postResp.json()
  const submissionId = String(postBody?.id ?? postBody?.submission?.id ?? postBody?.data?.id)
  expect(submissionId).toBeTruthy()

  // Login back as admin and open admin UI
  await page.click('button:has-text("Logout")')
  await loginAsAdmin(page)
  await page.goto('/admin')

  // Open Manage Submissions for the created game
  const strong = page.locator('strong', { hasText: gameName }).first()
  const gameContainer = strong.locator('xpath=ancestor::div[.//button[contains(normalize-space(.), "Manage Submissions")]][1]')
  const manageBtn = gameContainer.locator('button:has-text("Manage Submissions")')
  await expect(manageBtn).toBeVisible()
  await manageBtn.scrollIntoViewIfNeeded().catch(() => {})
  await manageBtn.click()

  const rows = page.locator('table tbody tr')
  await rows.first().waitFor({ timeout: 5000 })

  // Find the row for our submission by ID
  const count = await rows.count()
  let targetRow = null
  for (let i = 0; i < count; i++) {
    const row = rows.nth(i)
    const idText = (await row.locator('td').nth(0).textContent())?.trim() ?? ''
    if (idText === submissionId) { targetRow = row; break }
  }
  expect(targetRow).not.toBeNull()

  // Edit the submission score and save
  const newScore = `${originalScore}-edited`
  const input = targetRow!.locator('input').first()
  await input.fill(newScore)
  const putRespPromise = page.waitForResponse(r => r.url().endsWith(`/api/submissions/${submissionId}`) && r.request().method() === 'PUT')
  await targetRow!.locator('button:has-text("Save")').click()
  const putResp = await putRespPromise
  expect([200, 204]).toContain(putResp.status())

  // Verify updated score appears on the public submission detail page
  await page.goto(`/submission/${submissionId}`)
  await expect(page.locator(`text=Score: ${newScore}`)).toBeVisible()

  // Delete the submission via admin UI
  await page.goto('/admin')
  const strong2 = page.locator('strong', { hasText: gameName }).first()
  const gameContainer2 = strong2.locator('xpath=ancestor::div[.//button[contains(normalize-space(.), "Manage Submissions")]][1]')
  const manageBtn2 = gameContainer2.locator('button:has-text("Manage Submissions")')
  await manageBtn2.scrollIntoViewIfNeeded().catch(() => {})
  await manageBtn2.click()
  await rows.first().waitFor({ timeout: 5000 })

  // Locate row again and click its Delete button (confirm dialog)
  let found = false
  const rowCount2 = await rows.count()
  for (let i = 0; i < rowCount2; i++) {
    const row = rows.nth(i)
    const idText = (await row.locator('td').nth(0).textContent())?.trim() ?? ''
    if (idText === submissionId) {
      found = true
      const deleteBtn = row.locator('button:has-text("Delete")').first()
      await deleteBtn.scrollIntoViewIfNeeded().catch(() => {})

      let dialogSeen = false
      const delRespPromise = page.waitForResponse(r => r.url().endsWith(`/api/submissions/${submissionId}`) && r.request().method() === 'DELETE')
      page.once('dialog', async dialog => {
        dialogSeen = true
        expect(dialog.type()).toBe('confirm')
        expect(dialog.message()).toBe('Delete submission?')
        await dialog.accept()
      })

      try {
        await deleteBtn.click({ timeout: 10000 })
      } catch (e) {
        const handle = await deleteBtn.elementHandle()
        if (handle) await page.evaluate((el: any) => (el as HTMLElement).click(), handle).catch(() => {})
        else throw e
      }

      const delResp = await delRespPromise
      expect([200, 204]).toContain(delResp.status())
      expect(dialogSeen).toBeTruthy()
      break
    }
  }
  expect(found).toBeTruthy()

  // Verify submission detail returns Not found after deletion
  await page.goto(`/submission/${submissionId}`)
  await expect(page.locator('text=Not found')).toBeVisible()
})
