import { Page, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'
import { randomUUID } from 'crypto'

type Creds = { username: string; password: string }

function isCreds(obj: any): obj is Creds {
  return obj && typeof obj.username === 'string' && typeof obj.password === 'string'
}

async function readCredsFromFile(filePath: string) {
  const p = path.isAbsolute(filePath) ? filePath : path.resolve(__dirname, filePath)
  const raw = await fs.readFile(p, 'utf-8')
  return JSON.parse(raw) as Creds
}

async function resolveCreds(roleOrCreds?: any) : Promise<{creds: Creds, role?: 'admin'|'user'}> {
  // env overrides: if present, can be a JSON string or a path to a file
  const adminEnv = process.env.E2E_CREDENTIALS_ADMIN
  const userEnv = process.env.E2E_CREDENTIALS_USER

  if (roleOrCreds === 'admin' || roleOrCreds === undefined) {
    if (adminEnv) {
      try { return { creds: JSON.parse(adminEnv), role: 'admin' } }
      catch { /* treat as path */ }
      return { creds: await readCredsFromFile(adminEnv), role: 'admin' }
    }
    const creds = await readCredsFromFile('credentials.json')
    return { creds, role: 'admin' }
  }

  if (roleOrCreds === 'user' || roleOrCreds === 'normal') {
    if (userEnv) {
      try { return { creds: JSON.parse(userEnv), role: 'user' } }
      catch { /* treat as path */ }
      return { creds: await readCredsFromFile(userEnv), role: 'user' }
    }
    const creds = await readCredsFromFile('credentials-normal.json')
    return { creds, role: 'user' }
  }

  if (typeof roleOrCreds === 'string') {
    // treat as path to json file
    const creds = await readCredsFromFile(roleOrCreds)
    return { creds }
  }

  if (isCreds(roleOrCreds)) return { creds: roleOrCreds }

  throw new Error('Unable to resolve credentials')
}

export async function login(page: Page, roleOrCreds?: any, options?: { verifyRole?: boolean, expectedIsAdmin?: boolean }) {
  const { creds, role } = await resolveCreds(roleOrCreds)
  const { username, password } = creds
  const verifyRole = options?.verifyRole ?? true

  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', username)
  await page.fill('input[placeholder="Password"]', password)

  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().endsWith('/api/auth/login')),
    page.click('button[type="submit"]')
  ])
  expect(resp.status()).toBeGreaterThanOrEqual(200)
  expect(resp.status()).toBeLessThan(300)

  if (verifyRole) {
    // Prefer using the login response body (contains isAdmin). Fallback to /api/auth/me if needed.
    let loginBody: any = null
    try {
      loginBody = await resp.json()
    } catch {}

    if (loginBody) {
      if (typeof options?.expectedIsAdmin === 'boolean') {
        expect(loginBody.isAdmin).toBe(options!.expectedIsAdmin)
      }
    } else {
      throw new Error('Login response did not include isAdmin; expected backend to return isAdmin in POST /api/auth/login response')
    }
  }
}

export async function loginAsAdmin(page: Page, verifyRole = true) {
  return login(page, 'admin', { verifyRole })
}

export async function loginAsUser(page: Page, verifyRole = true) {
  return login(page, 'user', { verifyRole })
}

export async function createGame(page: Page, providedName?: string, options?: { navigateToGame?: boolean }) {
  const gameName = providedName ?? `e2e-game-${Date.now()}-${randomUUID()}`
  const navigateToGame = options?.navigateToGame !== false
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()

  await page.fill('label:has-text("Game name") >> xpath=.. >> input', gameName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', gameName)
  })
  await page.fill('input[type="time"]', '00:00')

  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().includes('/api/games') && (r.status() === 200 || r.status() === 201)),
    page.click('button:has-text("Create Game")')
  ])
  expect([200, 201]).toContain(resp.status())

  // Wait for SPA update after game creation then navigate via UI-only flow.
  await page.waitForTimeout(200)
  // Parse the POST response body if available (created game DTO)
  let createdBody: any = null
  try { createdBody = await resp.json() } catch {}
  const createdId = createdBody?.id ?? createdBody?.Id ?? createdBody?.gameId ?? createdBody?.GameId

  // If the caller doesn't want to navigate to the created game's page, return the created id (if available)
  if (!navigateToGame) {
    return { gameId: String(createdId), gameName }
  }

  // If we have the created id, directly navigate to the game's page to avoid relying on a separate list GET
  if (createdId) {
    await page.goto(`/games/${createdId}`)
    return { gameId: String(createdId), gameName }
  }

  // Fallback: navigate to home and click the visible link to reach the game's page
  await page.goto('/')
  await page.waitForResponse(r => r.url().includes('/api/games') && r.request().method() === 'GET')
  const gameLink = page.locator(`a:has-text("${gameName}")`).first()
  await expect(gameLink).toBeVisible({ timeout: 15000 })
  await gameLink.click()
  const url = page.url()
  const match = url.match(/\/games\/(\d+)/)
  if (!match) throw new Error('could not determine game id from URL: ' + url)
  return { gameId: match[1], gameName }
}
