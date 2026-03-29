import { test, expect } from '@playwright/test'
import { loginAsAdmin } from '../test-utils'

test('admin cannot see today\'s scores if he has not submitted yet', async ({ page }) => {
  // Admin: login and create a new game
  await loginAsAdmin(page)

  const gameName = `e2e-hide-today-${Date.now()}`
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()
  // Fill create form
  await page.fill('label:has-text("Game name") >> xpath=.. >> input', gameName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', gameName)
  })
  await page.fill('input[type="time"]', '00:00')
  // Click create and wait for the backend response; assert successful status
  const [createResp] = await Promise.all([
    page.waitForResponse(resp => resp.url().includes('/api/games')),
    page.click('button:has-text("Create Game")')
  ])
  const status = createResp.status()
  if (status < 200 || status >= 300) throw new Error(`Create Game failed with status ${status}`)

  // Navigate to home and click the created game to get to its page
  await page.waitForTimeout(500) // wait for SPA update after game creation
  await page.goto('/')
  await page.getByRole('link', { name: gameName }).click({ timeout: 10000 })
  // Now we should be on /games/:id
  const url = page.url()
  const match = url.match(/\/games\/(\d+)/)
  if (!match) throw new Error('could not determine game id from URL: ' + url)
  const gameId = match[1]

  // As admin (who has not submitted), visit the game's submissions page and assert today's scores are hidden
  await page.goto(`/games/${gameId}`)
  await expect(page.locator("text=Today's scores are hidden.")).toBeVisible()
})
