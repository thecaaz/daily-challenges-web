import { Page, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

export async function login(page: Page) {
  const credsPath = path.resolve(__dirname, 'credentials.json')
  const credsRaw = await fs.readFile(credsPath, 'utf-8')
  const { username, password } = JSON.parse(credsRaw)

  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', username)
  await page.fill('input[placeholder="Password"]', password)

  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/auth/login')),
    page.click('button[type="submit"]')
  ])
  expect(resp.status()).toBeGreaterThanOrEqual(200)
  expect(resp.status()).toBeLessThan(300)
}

export async function createGame(page: Page, providedName?: string) {
  const gameName = providedName ?? `e2e-game-${Date.now()}`
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()

  await page.fill('label:has-text("Game name") >> xpath=.. >> input', gameName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', gameName)
  })
  await page.fill('input[type="time"]', '00:00')

  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/games')),
    page.click('button:has-text("Create Game")')
  ])
  expect([200, 201]).toContain(resp.status())

  await page.goto('/')
  await page.click(`text=${gameName}`)
  const url = page.url()
  const match = url.match(/\/games\/(\d+)/)
  if (!match) throw new Error('could not determine game id from URL: ' + url)
  return { gameId: match[1], gameName }
}
