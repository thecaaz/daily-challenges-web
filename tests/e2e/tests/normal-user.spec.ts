import { test, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

test('normal user cannot see admin link and is redirected from /admin', async ({ page }) => {
  const credsPath = path.resolve(__dirname, '..', 'credentials-normal.json')
  const credsRaw = await fs.readFile(credsPath, 'utf-8')
  const creds = JSON.parse(credsRaw)

  // Register via UI
  await page.goto('/register')
  await page.fill('input[placeholder="Username"]', creds.username)
  await page.fill('input[placeholder="Password"]', creds.password)
  await page.click('button[type="submit"]')
  const regResp = await page.waitForResponse(resp => resp.url().endsWith('/api/auth/register'))
  expect(regResp.status()).toBe(200)

  // Login via UI
  await page.goto('/login')
  await page.fill('input[placeholder="Username"]', creds.username)
  await page.fill('input[placeholder="Password"]', creds.password)
  await page.click('button[type="submit"]')
  const loginResp = await page.waitForResponse(resp => resp.url().endsWith('/api/auth/login'))
  expect(loginResp.status()).toBe(200)

  // After login, the admin link should not be present
  const adminLink = page.locator('text=Admin')
  await expect(adminLink).toHaveCount(0)

  // Attempt to visit /admin should redirect away (Admin page shows 'Manage Games' when allowed)
  await page.goto('/admin')
  const manageGames = page.locator('text=Manage Games')
  await expect(manageGames).toHaveCount(0)
})
