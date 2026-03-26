import { Page, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

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
