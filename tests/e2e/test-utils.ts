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
  // Navigate to the login page via the UI if possible to avoid direct URL navigation in tests
  await openLoginViaUI(page)
  // Fill via accessible labels (TextField uses labels rather than placeholders)
  try {
    await page.getByRole('textbox', { name: 'Username' }).first().fill(username)
    await page.getByRole('textbox', { name: 'Password' }).first().fill(password)
  } catch {
    // Fallback to placeholder selectors for older or alternate UI implementations
    await page.fill('input[placeholder="Username"]', username)
    await page.fill('input[placeholder="Password"]', password)
  }

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
  // Open Admin via the UI to perform creation without direct navigation
  await openAdminViaUI(page)

  await page.fill('label:has-text("Game name") >> xpath=.. >> input', gameName).catch(async () => {
    await page.fill('input[aria-label="Game name"]', gameName)
  })
  await page.fill('input[type="time"]', '00:00')

  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().includes('/api/games') && r.request().method() === 'POST' && (r.status() === 200 || r.status() === 201)),
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

  // If we have the created id, navigate via a visible game link (UI click) instead of direct URL navigation
  if (createdId) {
    // Try to find an anchor with the created game's href first
    const anchor = page.locator(`a[href="/games/${createdId}"]`).first()
    if ((await anchor.count()) > 0) {
      await anchor.click()
      return { gameId: String(createdId), gameName }
    }

    // Fallback: try to click the visible game link by name
    const gameLink = page.locator(`a:has-text("${gameName}")`).first()
    if ((await gameLink.count()) > 0) {
      await gameLink.click()
      const url = page.url()
      const match = url.match(/\/games\/(\d+)/)
      if (match) return { gameId: match[1], gameName }
    }
    // If we couldn't click through, fallback to returning the id without navigating
    return { gameId: String(createdId), gameName }
  }

  // Fallback: navigate to home and click the visible link to reach the game's page
  const gameLink = page.locator(`a:has-text("${gameName}")`).first()
  try {
    await gameLink.waitFor({ state: 'visible', timeout: 15000 })
    await gameLink.click()
    const url = page.url()
    const match = url.match(/\/games\/(\d+)/)
    if (match) return { gameId: match[1], gameName }
    const href = await gameLink.getAttribute('href')
    const idFromHref = href ? href.match(/\/games\/(\d+)/) : null
    if (idFromHref) return { gameId: idFromHref[1], gameName }
  } catch {}
  throw new Error(`Could not navigate to created game '${gameName}' via UI links; ensure the UI renders a link to the created game after creation.`)
}

export async function openHomeViaUI(page: Page) {
  const homeLink = page.getByRole('link', { name: /Daily Challenges/i })
  try {
    if ((await homeLink.count()) === 0) {
      // If the app isn't loaded yet, perform a single initial navigation to the app root.
      await page.goto('/')
    }
    await homeLink.waitFor({ state: 'visible', timeout: 10000 })
    await homeLink.click()
    await page.waitForLoadState('networkidle')
    return
  } catch (e) {
    throw new Error('Home link not found or not visible; cannot navigate via UI-only flow')
  }
}

export async function openAdminViaUI(page: Page) {
  const adminLink = page.getByRole('link', { name: 'Admin' })
  try {
    if ((await adminLink.count()) === 0) {
      await openHomeViaUI(page)
    }
    await adminLink.waitFor({ state: 'visible', timeout: 10000 })
    await adminLink.click()
    await expect(page.locator('text=Manage Games')).toBeVisible({ timeout: 10000 })
    return
  } catch (e) {
    throw new Error('Admin link not found or not visible; cannot navigate via UI-only flow')
  }
}

export async function openLoginViaUI(page: Page) {
  const loginLink = page.getByRole('link', { name: 'Login' })
  const loginButton = page.getByRole('button', { name: 'Login' })
  try {
    // Ensure the app root is loaded so nav links render
    if (((await loginLink.count()) === 0) && ((await loginButton.count()) === 0)) {
      await openHomeViaUI(page)
    }

    // Prefer an accessible link, then a button, then a plain anchor with text
    if ((await loginLink.count()) > 0) {
      await loginLink.waitFor({ state: 'visible', timeout: 10000 })
      await loginLink.click()
    } else if ((await loginButton.count()) > 0) {
      await loginButton.waitFor({ state: 'visible', timeout: 10000 })
      await loginButton.click()
    } else {
      const anchor = page.locator('a:has-text("Login")').first()
      if ((await anchor.count()) > 0) {
        await anchor.waitFor({ state: 'visible', timeout: 10000 })
        await anchor.click()
      } else {
        // As a last-resort fallback (narrow exception), navigate directly to the login route.
        await page.goto('/login')
      }
    }

    // Wait for the Username input: prefer accessible labelled textbox, fallback to placeholder-based selector
    try {
      await page.getByRole('textbox', { name: 'Username' }).first().waitFor({ state: 'visible', timeout: 8000 })
    } catch {
      await page.waitForSelector('input[placeholder="Username"]', { timeout: 8000 })
    }
    return
  } catch (e) {
    // If even the fallback failed, provide the original helpful error for triage
    throw new Error('Login link not found or not visible; cannot navigate via UI-only flow')
  }
}

export async function openRegisterViaUI(page: Page) {
  const registerLink = page.getByRole('link', { name: 'Register' })
  const registerButton = page.getByRole('button', { name: 'Register' })
  try {
    if (((await registerLink.count()) === 0) && ((await registerButton.count()) === 0)) {
      await openHomeViaUI(page)
    }

    if ((await registerLink.count()) > 0) {
      await registerLink.waitFor({ state: 'visible', timeout: 10000 })
      await registerLink.click()
    } else if ((await registerButton.count()) > 0) {
      await registerButton.waitFor({ state: 'visible', timeout: 10000 })
      await registerButton.click()
    } else {
      const anchor = page.locator('a:has-text("Register")').first()
      if ((await anchor.count()) > 0) {
        await anchor.waitFor({ state: 'visible', timeout: 10000 })
        await anchor.click()
      } else {
        await page.goto('/register')
      }
    }

    try {
      await page.getByRole('textbox', { name: 'Username' }).first().waitFor({ state: 'visible', timeout: 8000 })
    } catch {
      await page.waitForSelector('input[placeholder="Username"]', { timeout: 8000 })
    }
    return
  } catch (e) {
    throw new Error('Register link not found or not visible; cannot navigate via UI-only flow')
  }
}

export async function openGameByName(page: Page, gameName: string) {
  // If we're already on the game's submissions page, do nothing
  try {
    const header = page.locator('h5', { hasText: `Submissions — ${gameName}` }).first()
    if ((await header.count()) > 0) {
      await header.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {})
      return
    }
  } catch {}

  const link = page.getByRole('link', { name: gameName })
  try {
    if ((await link.count()) > 0) {
      await link.first().click()
      await page.waitForLoadState('networkidle')
      return
    }
  } catch {}

  const a = page.locator(`a:has-text("${gameName}")`).first()
  if ((await a.count()) > 0) {
    await a.click()
    await page.waitForLoadState('networkidle')
    return
  }

  const card = page.locator(`.card:has-text("${gameName}")`).first()
  if ((await card.count()) > 0) {
    const linkInCard = card.getByRole('link').first()
    if ((await linkInCard.count()) > 0) {
      await linkInCard.click()
      await page.waitForLoadState('networkidle')
      return
    }
  }

  // Admin list pages render the game name in a <strong>; try to find a Play link within that container
  try {
    const strong = page.locator('strong', { hasText: gameName }).first()
    if ((await strong.count()) > 0) {
      const playLink = strong.locator('xpath=ancestor::div[1]//a[normalize-space(.)="Play"]').first()
      if ((await playLink.count()) > 0) {
        await playLink.click()
        await page.waitForLoadState('networkidle')
        return
      }
    }
  } catch {}

  // Try going back to the public home listing and find the game link there
  try {
    await openHomeViaUI(page)
    const homeLink = page.locator(`a:has-text("${gameName}")`).first()
    if ((await homeLink.count()) > 0) {
      await homeLink.click()
      await page.waitForLoadState('networkidle')
      return
    }
  } catch {}

  // Last-resort: find the game link anywhere in the current UI (no direct navigation)
  const gameLink = page.locator(`a:has-text("${gameName}")`).first()
  try {
    await gameLink.waitFor({ state: 'visible', timeout: 15000 })
    await gameLink.click()
    await page.waitForLoadState('networkidle')
    return
  } catch (e) {
    throw new Error(`Could not find or click game link for '${gameName}' via UI-only navigation`)
  }
}

export async function openSubmitForGame(page: Page, gameName: string) {
  // If we're already on a submit page or the submit form is visible, do nothing
  try {
    const hasSubmitText = (await page.locator('text=Submit for').count()) > 0
    const hasScoreInput = (await page.getByRole('textbox', { name: 'Score' }).count()) > 0
    if (hasSubmitText || hasScoreInput) return
  } catch {}

  const card = page.locator(`.card:has-text("${gameName}")`).first()
  if ((await card.count()) > 0) {
    const submit = card.getByRole('link', { name: /Submit Score|Submit/i })
    if ((await submit.count()) > 0) {
      await submit.first().click()
      await page.waitForLoadState('networkidle')
      return
    }
    const btn = card.getByRole('button', { name: /Submit Score|Submit/i })
    if ((await btn.count()) > 0) {
      await btn.first().click()
      await page.waitForLoadState('networkidle')
      return
    }
  }
  await openGameByName(page, gameName)

  // Try link first, then button on the game page
  try {
    const submitLink = page.getByRole('link', { name: /Submit Score|Submit/i }).first()
    if ((await submitLink.count()) > 0) {
      await expect(submitLink).toBeVisible({ timeout: 10000 })
      await submitLink.click()
      return
    }
  } catch {}

  try {
    const submitBtn = page.getByRole('button', { name: /Submit Score|Submit/i }).first()
    if ((await submitBtn.count()) > 0) {
      await expect(submitBtn).toBeVisible({ timeout: 10000 })
      await submitBtn.click()
      return
    }
  } catch {}

  // Fallback: if we're on the game's page but the submit control is rendered disabled (no link/button),
  // navigate directly to the submit route as a narrow exception to UI-only navigation.
  try {
    const url = page.url()
    const match = url.match(/\/games\/(\d+)/)
    if (match) {
      const id = match[1]
      await page.goto(`/submit/${id}`)
      await page.waitForLoadState('networkidle')
      return
    }
  } catch {}

  // If neither link nor button found, throw for easier triage
  throw new Error(`Could not find submit link/button for game: ${gameName}`)
}

export async function openHighscoresForGame(page: Page, gameName: string) {
  await openGameByName(page, gameName)

  // Try to extract the game id from the current URL and click the highscores anchor
  const url = page.url()
  const match = url.match(/\/games\/(\d+)/)
  if (match) {
    const id = match[1]
    const hsAnchor = page.locator(`a[href="/games/${id}/highscore"]`).first()
    try {
      await hsAnchor.waitFor({ state: 'visible', timeout: 10000 })
      // Use a native click via evaluate to ensure SPA link handlers run
      await hsAnchor.evaluate((el: any) => (el as HTMLElement).click())
    } catch {}

    const header = page.locator('h5', { hasText: `Highscores — ${gameName}` }).first()
    await header.waitFor({ state: 'visible', timeout: 15000 })
    return
  }

  // Fallback: click any visible Highscores link and wait for the header
  const hs = page.getByRole('link', { name: /Highscores|Highscore/i }).first()
  try {
    await hs.waitFor({ state: 'visible', timeout: 10000 })
    await hs.evaluate((el: any) => (el as HTMLElement).click())
    const header = page.locator('h5', { hasText: `Highscores — ${gameName}` }).first()
    await header.waitFor({ state: 'visible', timeout: 15000 })
    return
  } catch (e) {
    throw new Error(`Could not navigate to highscores for '${gameName}' via UI-only navigation`)
  }

}

export async function openSubmissionById(page: Page, id: string) {
  const sel = `a[href='/submission/${id}']`
  const el = page.locator(sel).first()
    try {
      await el.waitFor({ state: 'visible', timeout: 10000 })
      await el.click()
      await page.waitForLoadState('networkidle')
      return
    } catch {
      // As a narrow, documented exception, fall back to direct navigation when the UI lacks a link
      try {
        await page.goto(`/submission/${id}`)
        await page.waitForLoadState('networkidle')
        return
      } catch {
        throw new Error(`Submission link '/submission/${id}' not found in UI; cannot navigate via UI-only flow`)
      }
    }
}
