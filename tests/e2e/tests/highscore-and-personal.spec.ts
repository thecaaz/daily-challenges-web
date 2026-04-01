import { test, expect } from '@playwright/test'
import { loginAsAdmin, loginAsUser, createGame, openRegisterViaUI, login, openSubmitForGame, openHighscoresForGame, openGameByName, openSubmissionById } from '../test-utils'
import { randomUUID } from 'crypto'

test('highscore list and personal highscores display and order correctly via UI', async ({ page }) => {
  // Create a game as admin
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page)

  // Register a separate user (userA) and submit a score as that user
  await page.click('button:has-text("Logout")').catch(() => {})
  const userA = `e2e-guest-${Date.now()}-${randomUUID()}`
  const userAPass = `Pass-${Date.now() % 10000}`
  await openRegisterViaUI(page)
  await page.getByRole('textbox', { name: 'Username' }).fill(userA)
  await page.getByRole('textbox', { name: 'Password' }).fill(userAPass)
  await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/auth/register') && r.request().method() === 'POST'),
    page.click('button[type="submit"]')
  ])

  // Login as the newly registered user and submit score 200
  await login(page, { username: userA, password: userAPass })
  await openSubmitForGame(page, gameName)
  const anonScore = '200'
  await page.getByRole('textbox', { name: 'Score' }).fill(anonScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', anonScore)
  })
  const [anonResp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Submit' }).click()
  ])
  expect([200, 201]).toContain(anonResp.status())

  // Login as normal user and create one submission (250)
  await page.click('button:has-text("Logout")').catch(() => {})
  await loginAsUser(page)
  await openSubmitForGame(page, gameName)
  const userScore = '250'
  await page.getByRole('textbox', { name: 'Score' }).fill(userScore).catch(async () => {
    await page.fill('input[placeholder="Score"]', userScore)
  })
  const [userResp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Submit' }).click()
  ])
  expect([200, 201]).toContain(userResp.status())

  // Logout user and create an admin submission that should top the leaderboard
  await page.click('button:has-text("Logout")').catch(() => {})
  await loginAsAdmin(page)
  await openSubmitForGame(page, gameName)
  const adminTop = '300'
  await page.getByRole('textbox', { name: 'Score' }).fill(adminTop).catch(async () => {
    await page.fill('input[placeholder="Score"]', adminTop)
  })
  const [adminResp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Submit' }).click()
  ])
  expect([200, 201]).toContain(adminResp.status())

  // Finally, view the highscores as the logged-in normal user (so todays scores are visible)
  await page.click('button:has-text("Logout")').catch(() => {})
  await loginAsUser(page)
  await openHighscoresForGame(page, gameName)

  const header = page.locator('h5', { hasText: `Highscores — ${gameName}` }).first()
  await expect(header).toBeVisible({ timeout: 15000 })
  const grid = header.locator('xpath=following::div[contains(@class,"MuiGrid-container")][1]')
  await expect(grid).toBeVisible({ timeout: 15000 })
  const cards = grid.locator('a')
  const cardCount = await cards.count()
  expect(cardCount).toBeGreaterThanOrEqual(3)

  // Read scores by opening each submission detail (more reliable than parsing card internals)
  const scores: number[] = []
  const topN = Math.min(3, cardCount)
  const hrefs: string[] = []
  for (let i = 0; i < topN; i++) {
    const link = cards.nth(i)
    const href = await link.getAttribute('href')
    if (!href) throw new Error('could not determine submission link href')
    hrefs.push(href)
  }

  for (let i = 0; i < hrefs.length; i++) {
    // Click the submission anchor to open detail via UI
    const link = cards.nth(i)
    await link.click()
    const scoreLocator = page.locator('text=/Score:\\s*-?\\d+(?:[.,]\\d+)?/').first()
    await expect(scoreLocator).toBeVisible()
    const scoreRaw = (await scoreLocator.textContent()) ?? ''
    const mm = scoreRaw.match(/-?\d+(?:[.,]\d+)?/)
    const num = mm ? Math.round(parseFloat(mm[0].replace(',', '.'))) : NaN
    scores.push(num)
    // go back to highscores for next item via UI
    await openHighscoresForGame(page, gameName)
    await expect(page.locator('h5', { hasText: `Highscores — ${gameName}` })).toBeVisible({ timeout: 15000 })
  }
  // Expect descending order: adminTop, userScore2, anonScore
  expect(scores[0]).toBe(parseInt(adminTop))
  expect(scores[1]).toBe(parseInt(userScore))
  expect(scores[2]).toBe(parseInt(anonScore))

  // Check personal highs for the logged-in user
  await openGameByName(page, gameName)
  const yourHs = page.getByRole('link', { name: 'Your Highscores' })
  await expect(yourHs).toBeVisible()
  await yourHs.click()
  const pHeader = page.locator('h5', { hasText: `Your Highscores — ${gameName}` }).first()
  await expect(pHeader).toBeVisible({ timeout: 15000 })
  const pGrid = pHeader.locator('xpath=following::div[contains(@class,"MuiGrid-container")][1]')
  await expect(pGrid).toBeVisible({ timeout: 15000 })
  const pCards = pGrid.locator('a')
  const pCount = await pCards.count()
  expect(pCount).toBeGreaterThanOrEqual(1)
  // The score is rendered in a Typography variant="h6" element inside the card.
  // The card may contain multiple headings (username and score). Find the
  // first heading that contains a numeric score.
  const h6s = pCards.nth(0).locator('h6')
  const h6count = await h6s.count()
  let foundScore: number | null = null
  for (let i = 0; i < h6count; i++) {
    const txt = (await h6s.nth(i).textContent()) ?? ''
    const mm = txt.match(/^\s*-?\d+(?:[.,]\d+)?\s*$/)
    if (mm) { foundScore = Math.round(parseFloat(mm[0].replace(',', '.'))); break }
  }
  expect(foundScore).not.toBeNull()
  expect(foundScore).toBe(parseInt(userScore))
})
