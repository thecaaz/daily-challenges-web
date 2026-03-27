import { test, expect } from '@playwright/test'
import { loginAsAdmin, loginAsUser, createGame } from '../test-utils'

test('highscore list and personal highscores display and order correctly via UI', async ({ page }) => {
  // Create a game as admin
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page)

  // Register a separate user (userA) and submit a score as that user
  await page.click('button:has-text("Logout")').catch(() => {})
  const userA = `e2e-guest-${Date.now()}`
  const userAPass = `Pass-${Date.now() % 10000}`
  await page.goto('/register')
  await page.fill('input[placeholder="Username"]', userA)
  await page.fill('input[placeholder="Password"]', userAPass)
  await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/auth/register') && r.request().method() === 'POST'),
    page.click('button[type="submit"]')
  ])

  // Login as the newly registered user and submit score 200
  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', userA)
  await page.fill('input[placeholder="Password"]', userAPass)
  await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/auth/login') && r.request().method() === 'POST'),
    page.click('button[type="submit"]')
  ])
  await page.goto(`/submit/${gameId}`)
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
  await page.goto(`/submit/${gameId}`)
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
  await page.goto(`/submit/${gameId}`)
  const adminTop = '300'
  await page.getByRole('textbox', { name: 'Score' }).fill(adminTop).catch(async () => {
    await page.fill('input[placeholder="Score"]', adminTop)
  })
  const [adminResp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/submissions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Submit' }).click()
  ])
  expect([200, 201]).toContain(adminResp.status())

  // Finally, view the highscores as the logged-in normal user (so today's scores are visible)
  await page.click('button:has-text("Logout")').catch(() => {})
  await loginAsUser(page)
  await page.goto(`/games/${gameId}/highscore`)

  const header = page.locator('h5', { hasText: `Highscores — ${gameName}` }).first()
  await expect(header).toBeVisible()
  const grid = header.locator('xpath=following::div[contains(@class,"MuiGrid-container")][1]')
  await expect(grid).toBeVisible()
  const cards = grid.locator('div.card')
  const cardCount = await cards.count()
  expect(cardCount).toBeGreaterThanOrEqual(3)

  // Read scores by opening each submission detail (more reliable than parsing card internals)
  const scores: number[] = []
  const topN = Math.min(3, cardCount)
  const hrefs: string[] = []
  for (let i = 0; i < topN; i++) {
    const link = cards.nth(i).locator('xpath=ancestor::a[1]').first()
    const href = await link.getAttribute('href')
    if (!href) throw new Error('could not determine submission link href')
    hrefs.push(href)
  }

  for (let i = 0; i < hrefs.length; i++) {
    const m = hrefs[i].match(/\/submission\/(\d+)/)
    if (!m) throw new Error('invalid submission href: ' + hrefs[i])
    const id = m[1]
    await page.goto(`/submission/${id}`)
    const scoreLocator = page.locator('text=/Score:\\s*-?\\d+(?:[.,]\\d+)?/').first()
    await expect(scoreLocator).toBeVisible()
    const scoreRaw = (await scoreLocator.textContent()) ?? ''
    const mm = scoreRaw.match(/-?\d+(?:[.,]\d+)?/)
    const num = mm ? Math.round(parseFloat(mm[0].replace(',', '.'))) : NaN
    scores.push(num)
    // go back to highscores for next item
    await page.goto(`/games/${gameId}/highscore`)
    await expect(page.locator('h5', { hasText: `Highscores — ${gameName}` })).toBeVisible()
  }
  // Expect descending order: adminTop, userScore2, anonScore
  expect(scores[0]).toBe(parseInt(adminTop))
  expect(scores[1]).toBe(parseInt(userScore))
  expect(scores[2]).toBe(parseInt(anonScore))

  // Check personal highs for the logged-in user
  await page.goto(`/games/${gameId}/personal-highscore`)
  const pHeader = page.locator('h5', { hasText: `Your Highscores — ${gameName}` }).first()
  await expect(pHeader).toBeVisible()
  const pGrid = pHeader.locator('xpath=following::div[contains(@class,"MuiGrid-container")][1]')
  await expect(pGrid).toBeVisible()
  const pCards = pGrid.locator('div.card')
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
