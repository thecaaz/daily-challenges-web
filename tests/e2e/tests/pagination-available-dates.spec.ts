import { test, expect } from '@playwright/test'
import { loginAsAdmin, createGame, openGameByName } from '../test-utils'

// Increase per-file test timeout to reduce flakiness in CI
test.setTimeout(60_000)

test('pagination, available dates, and page metadata in UI', async ({ page }) => {
  await loginAsAdmin(page)
  const { gameId, gameName } = await createGame(page, undefined, { navigateToGame: false })

  // Stub the overview endpoint so the page shows available dates and hasSubmitted flag
  await page.route(`**/api/games/${gameId}/overview*`, async route => {
    const body = {
      game: { id: Number(gameId), name: gameName, currentScoringDay: '2026-03-27' },
      availableDates: ['2026-03-27', '2026-03-26'],
      hasSubmittedForLatest: true
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
  })

  // Intercept submission list requests and return a controlled paged dataset
  await page.route(`**/api/submissions/game/${gameId}/available-dates`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(['2026-03-27', '2026-03-26']) })
  })

  // Respond to lightweight has-submitted check used by the UI so the page shows today's scores
  await page.route(`**/api/submissions/game/${gameId}/has-submitted*`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ hasSubmittedForLatest: true }) })
  })

  await page.route(`**/api/submissions/game/${gameId}*`, async route => {
    const req = route.request()
    const url = new URL(req.url())
    const pageParam = url.searchParams.get('page') || '1'
    const scoringDayParam = url.searchParams.get('scoringDay') || null

    // If caller requested a specific scoringDay, return the items for that day
    if (scoringDayParam) {
      if (scoringDayParam === '2026-03-26') {
        const items = [
          { id: 1003, gameId: Number(gameId), userId: null, score: '30', username: 'UserC', screenshotUrl: null, createdAt: '2026-03-26T10:00:00Z', scoringDay: '2026-03-26' },
          { id: 1004, gameId: Number(gameId), userId: null, score: '40', username: 'UserD', screenshotUrl: null, createdAt: '2026-03-26T11:00:00Z', scoringDay: '2026-03-26' }
        ]
        const body = { items, hasSubmittedForLatest: true, hasMore: false, page: 1, pageSize: 2, totalCount: 2, totalPages: 1 }
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
        return
      }
      if (scoringDayParam === '2026-03-27') {
        const items = [
          { id: 1001, gameId: Number(gameId), userId: null, score: '10', username: 'UserA', screenshotUrl: null, createdAt: '2026-03-27T10:00:00Z', scoringDay: '2026-03-27' },
          { id: 1002, gameId: Number(gameId), userId: null, score: '20', username: 'UserB', screenshotUrl: null, createdAt: '2026-03-27T11:00:00Z', scoringDay: '2026-03-27' }
        ]
        const body = { items, hasSubmittedForLatest: true, hasMore: false, page: 1, pageSize: 2, totalCount: 2, totalPages: 1 }
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
        return
      }
    }

    if (pageParam === '1') {
      const items = [
        { id: 1001, gameId: Number(gameId), userId: null, score: '10', username: 'UserA', screenshotUrl: null, createdAt: '2026-03-27T10:00:00Z', scoringDay: '2026-03-27' },
        { id: 1002, gameId: Number(gameId), userId: null, score: '20', username: 'UserB', screenshotUrl: null, createdAt: '2026-03-27T11:00:00Z', scoringDay: '2026-03-27' }
      ]
      const body = { items, hasSubmittedForLatest: true, hasMore: true, page: 1, pageSize: 2, totalCount: 4, totalPages: 2 }
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
      return
    }

    if (pageParam === '2') {
      const items = [
        { id: 1003, gameId: Number(gameId), userId: null, score: '30', username: 'UserC', screenshotUrl: null, createdAt: '2026-03-26T10:00:00Z', scoringDay: '2026-03-26' },
        { id: 1004, gameId: Number(gameId), userId: null, score: '40', username: 'UserD', screenshotUrl: null, createdAt: '2026-03-26T11:00:00Z', scoringDay: '2026-03-26' }
      ]
      const body = { items, hasSubmittedForLatest: true, hasMore: false, page: 2, pageSize: 2, totalCount: 4, totalPages: 2 }
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
      return
    }

    // fallback
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [], hasSubmittedForLatest: false, hasMore: false, page: Number(pageParam), pageSize: 2, totalCount: 4, totalPages: 2 }) })
  })

  // Open the game's submissions page via UI
  await openGameByName(page, gameName)
  await expect(page.locator('text=Submissions —')).toBeVisible()

  // Open Day combobox and select 'All' so load-more will display items from other dates
  await page.getByRole('combobox', { name: 'Day' }).click()
  await page.getByRole('option', { name: 'All' }).click()

  // Assert initial page items (page 1) are visible within submission cards after selecting 'All'
  await expect(page.locator('div.card:has-text("10")').first()).toBeVisible()
  await expect(page.locator('div.card:has-text("20")').first()).toBeVisible()

  // Click 'Load more' to fetch page 2 and assert appended items are visible
  const [loadResp] = await Promise.all([
    page.waitForResponse(r => r.url().includes(`/api/submissions/game/${gameId}`) && r.request().method() === 'GET' && r.url().includes('page=2')),
    page.click('button:has-text("Load more")')
  ])
  expect(loadResp.status()).toBeGreaterThanOrEqual(200)
  await expect(page.locator('div.card:has-text("30")').first()).toBeVisible()
  await expect(page.locator('div.card:has-text("40")').first()).toBeVisible()

  // After loading page 2, the Load more button should no longer be present
  await expect(page.locator('button:has-text("Load more")')).toHaveCount(0)

  // Now filter by the older date and assert only that day's items are shown
  await page.getByRole('combobox', { name: 'Day' }).click()
  await page.getByRole('option', { name: '2026-03-26' }).click()
  await expect(page.locator('div.card:has-text("30")').first()).toBeVisible()
  await expect(page.locator('div.card:has-text("10")')).toHaveCount(0)
})
