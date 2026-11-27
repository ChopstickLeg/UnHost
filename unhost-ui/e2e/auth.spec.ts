import type { Page } from '@playwright/test'
import { expect, test } from './fixtures'

const email = 'browser@example.com'
const password = 'Browser-Test123!'

async function fillAccount(page: Page) {
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Password', { exact: true }).fill(password)
}

test('operator completes setup, restores the session after recreation, and signs out', async ({
  page,
  browser,
  installation,
}) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Set up UnHost', exact: true })).toBeVisible()
  await expect(page).toHaveURL(`${installation.url}/setup`)
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeHidden()
  await fillAccount(page)
  await page.getByLabel('Setup key', { exact: true }).fill(installation.bootstrapSecret)
  await page.getByRole('button', { name: 'Create account', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible()
  await expect(page.getByRole('status')).toHaveText('Account created. Sign in to continue.')
  await expect(page).toHaveURL(`${installation.url}/login?created=1`)
  await fillAccount(page)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible()
  await expect(page.getByText(email, { exact: true })).toBeVisible()
  await expect(page).toHaveURL(`${installation.url}/`)
  await expect(page.getByRole('heading', { name: 'Local cluster', exact: true })).toBeVisible()
  await expect(page.getByText('Disconnected', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Initialize Swarm' })).toBeHidden()
  await page.goto('/setup')
  await expect(page).toHaveURL(`${installation.url}/`)
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible()

  const anonymousContext = await browser.newContext()
  try {
    const anonymousPage = await anonymousContext.newPage()
    await anonymousPage.goto(`${installation.url}/`)
    await expect(anonymousPage).toHaveURL(`${installation.url}/login`)
    await expect(anonymousPage.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible()
    await expect(
      anonymousPage.getByRole('heading', { name: 'Dashboard', exact: true }),
    ).toBeHidden()
    await expect(anonymousPage.getByLabel('Setup key', { exact: true })).toBeHidden()
  } finally {
    await anonymousContext.close()
  }

  await page.reload()
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible()
  await installation.recreate()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible()
  await expect(page.getByText(email, { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Sign out', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible()
  await expect(page).toHaveURL(`${installation.url}/login`)
  await page.goto('/')
  await expect(page).toHaveURL(`${installation.url}/login`)
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeHidden()
})

test('invalid URLs show the 404 page while API and asset misses stay 404', async ({
  page,
  installation,
}) => {
  await page.goto('/does/not/exist')
  await expect(
    page.getByRole('heading', { name: '404 · Page not found', exact: true }),
  ).toBeVisible()
  await expect(page).toHaveURL(`${installation.url}/does/not/exist`)
  await page.reload()
  await expect(
    page.getByRole('heading', { name: '404 · Page not found', exact: true }),
  ).toBeVisible()
  await page.getByRole('link', { name: 'Go home', exact: true }).click()
  await expect(page).toHaveURL(`${installation.url}/setup`)
  await expect(page.getByRole('heading', { name: 'Set up UnHost', exact: true })).toBeVisible()
  await page.goto('/login')
  await expect(page).toHaveURL(`${installation.url}/setup`)

  for (const path of ['/api/does-not-exist', '/assets/does-not-exist.js']) {
    const response = await page.request.get(path)
    expect(response.status()).toBe(404)
    expect(response.headers()['content-type'] ?? '').not.toContain('text/html')
  }
})

test('operator sees bootstrap, password, and sign-in errors without gaining access', async ({
  page,
  installation,
}) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Set up UnHost', exact: true })).toBeVisible()
  await fillAccount(page)
  await page.getByLabel('Setup key', { exact: true }).fill('incorrect-bootstrap')
  await page.getByRole('button', { name: 'Create account', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('Check your setup key')
  await expect(page.getByRole('heading', { name: 'Set up UnHost', exact: true })).toBeVisible()

  await page.getByLabel('Setup key', { exact: true }).fill(installation.bootstrapSecret)
  await page.getByLabel('Password', { exact: true }).fill('lowercase')
  await page.getByRole('button', { name: 'Create account', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('Passwords must have')
  await expect(page.getByRole('heading', { name: 'Set up UnHost', exact: true })).toBeVisible()

  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Create account', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible()
  await fillAccount(page)
  await page.getByLabel('Password', { exact: true }).fill('incorrect-password')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page.getByRole('alert')).toHaveText('The email or password is incorrect.')
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeHidden()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible()
  await fillAccount(page)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible()
})
