import { test, expect } from '@playwright/test'
import fs from 'fs/promises'
import path from 'path'

test('creates first user through UI and loads admin page', async ({ page }) => {
  const credsPath = path.resolve(__dirname, '..', 'credentials.json')
  const credsRaw = await fs.readFile(credsPath, 'utf-8')
  const creds = JSON.parse(credsRaw)

  // Register via UI
  await page.goto('/register')
  await page.fill('input[placeholder="Username"]', creds.username)
  await page.fill('input[placeholder="Password"]', creds.password)
  await page.click('button[type="submit"]')
  const regResp = await page.waitForResponse(resp => resp.url().endsWith('/api/auth/register'))
  expect(regResp.status()).toBe(200)
  // If the app didn't automatically redirect, go to login explicitly
  await page.goto('/login')

  // Should be redirected to login; perform login via UI
  await page.fill('input[placeholder="Username"]', creds.username)
  await page.fill('input[placeholder="Password"]', creds.password)
  await page.click('button[type="submit"]')
  const loginResp = await page.waitForResponse(resp => resp.url().endsWith('/api/auth/login'))
  expect(loginResp.status()).toBe(200)
  await page.goto('/')

  // Navigate to admin page and verify admin-only content
  await page.goto('/admin')
  await expect(page.locator('text=Manage Games')).toBeVisible()
})
